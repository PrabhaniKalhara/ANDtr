using ANDtrSafetyMvc.Data;
using ANDtrSafetyMvc.Models;
using ANDtrSafetyMvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ANDtrSafetyMvc.Controllers;

[Authorize(Policy = "Customers")]
public class SharedReviewsController(AppDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var shares = await db.ReviewShares
            .Where(s => s.CustomerId == CurrentUserId && s.IsActive)
            .Include(s => s.Review).ThenInclude(r => r!.Employee)
            .Include(s => s.Review).ThenInclude(r => r!.Items)
            .OrderByDescending(s => s.SharedAt)
            .AsNoTracking().ToListAsync();
        return View(shares);
    }

    public async Task<IActionResult> Details(int id)
    {
        var share = await db.ReviewShares
            .Include(s => s.Review).ThenInclude(r => r!.Employee)
            .Include(s => s.Review).ThenInclude(r => r!.Items).ThenInclude(i => i.Notice)
            .Include(s => s.Review).ThenInclude(r => r!.Items).ThenInclude(i => i.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(s => s.ShareId == id && s.CustomerId == CurrentUserId && s.IsActive);
        if (share?.Review is null) return NotFound();

        foreach (var item in share.Review.Items)
        {
            item.Comments = item.Comments.Where(comment =>
                (comment.CommentType == "Customer comment" && comment.AuthorId == CurrentUserId) ||
                (comment.CommentType == "Finding" && share.IncludeFindings) ||
                (comment.CommentType == "Internal employee comment" && share.IncludeInternalEmployeeComments) ||
                (comment.CommentType == "Internal company note" && share.IncludeInternalCompanyNotes))
                .ToList();
        }
        return View(new SharedReviewDetailsViewModel { Share = share, Review = share.Review });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int shareId, int reviewNoticeId, string commentText)
    {
        var share = await db.ReviewShares.Include(s => s.Review).FirstOrDefaultAsync(s => s.ShareId == shareId && s.CustomerId == CurrentUserId && s.IsActive);
        if (share?.Review is null) return NotFound();
        var item = await db.ReviewNotices.Include(i => i.Review).FirstOrDefaultAsync(i => i.ReviewNoticeId == reviewNoticeId && i.ReviewId == share.ReviewId);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Length > 3000)
        {
            TempData["Error"] = "Enter a comment of up to 3,000 characters.";
            return RedirectToAction(nameof(Details), new { id = shareId });
        }

        var customer = await users.FindByIdAsync(CurrentUserId);
        var comment = new ReviewComment { ReviewNoticeId = reviewNoticeId, AuthorId = CurrentUserId, CommentType = "Customer comment", CommentText = commentText.Trim() };
        db.ReviewComments.Add(comment);
        db.Notifications.Add(new AppNotification
        {
            EmployeeId = share.Review.EmployeeId,
            Comment = comment,
            Message = $"{customer?.FullName ?? "A customer"} commented on the shared review '{share.Review.Title}'."
        });
        share.Review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Your comment was sent to the review owner.";
        return RedirectToAction(nameof(Details), new { id = shareId });
    }
}
