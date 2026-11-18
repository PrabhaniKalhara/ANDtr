using ANDtrSafetyMvc.Data;
using ANDtrSafetyMvc.Models;
using ANDtrSafetyMvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ANDtrSafetyMvc.Controllers;

[Authorize(Policy = "Employees")]
public class ReviewsController(AppDbContext db) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(ReviewListViewModel filter)
    {
        var query = db.Reviews.Include(r => r.Employee).Include(r => r.Items).AsQueryable();
        if (!User.IsInRole("Admin")) query = query.Where(r => r.EmployeeId == CurrentUserId);
        query = query.Where(r => r.IsArchived == filter.ShowArchived);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(r => r.Title.Contains(filter.Search) || (r.Description != null && r.Description.Contains(filter.Search)));
        query = filter.SortBy switch
        {
            "title" => query.OrderBy(r => r.Title),
            "created" => query.OrderByDescending(r => r.CreatedAt),
            "items" => query.OrderByDescending(r => r.Items.Count),
            _ => query.OrderByDescending(r => r.UpdatedAt)
        };
        filter.Reviews = await query.AsNoTracking().ToListAsync();
        return View(filter);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateReviewViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReviewViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var review = new Review
        {
            EmployeeId = CurrentUserId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = "In progress"
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        TempData["Success"] = "Risk review created. Add notices from the search results.";
        return RedirectToAction(nameof(Details), new { id = review.ReviewId });
    }

    public async Task<IActionResult> Details(int id)
    {
        var review = await LoadReviewAsync(id);
        if (review is null) return NotFound();
        if (!CanManage(review)) return Forbid();
        return View(new ReviewDetailsViewModel { Review = review, CanEdit = true });
    }

    [HttpGet]
    public async Task<IActionResult> AddNotice(int id)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == id);
        if (review is null) return NotFound();
        if (!CanManage(review)) return Forbid();
        ViewBag.Notices = await db.Notices.OrderByDescending(n => n.DatePublished).ToListAsync();
        return View(review);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNotice(int reviewId, int noticeId)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
        if (review is null) return NotFound();
        if (!CanManage(review)) return Forbid();
        if (!await db.Notices.AnyAsync(n => n.NoticeId == noticeId)) return NotFound();
        if (await db.ReviewNotices.AnyAsync(i => i.ReviewId == reviewId && i.NoticeId == noticeId))
            TempData["Error"] = "That notice is already in this review.";
        else
        {
            db.ReviewNotices.Add(new ReviewNotice { ReviewId = reviewId, NoticeId = noticeId, RiskLevel = "Not rated" });
            review.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            TempData["Success"] = "Notice added to the review.";
        }
        return RedirectToAction(nameof(Details), new { id = reviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int reviewNoticeId, string riskLevel)
    {
        var item = await db.ReviewNotices.Include(i => i.Review).FirstOrDefaultAsync(i => i.ReviewNoticeId == reviewNoticeId);
        if (item is null) return NotFound();
        if (item.Review is null || !CanManage(item.Review)) return Forbid();
        if (!new[] { "Low", "Medium", "High", "Not rated" }.Contains(riskLevel)) return BadRequest("Invalid risk level.");
        item.RiskLevel = riskLevel;
        item.Review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Risk rating saved.";
        return RedirectToAction(nameof(Details), new { id = item.ReviewId });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmRemove(int id)
    {
        var item = await db.ReviewNotices.Include(i => i.Review).Include(i => i.Notice).FirstOrDefaultAsync(i => i.ReviewNoticeId == id);
        if (item is null) return NotFound();
        if (item.Review is null || !CanManage(item.Review)) return Forbid();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveNotice(int id)
    {
        var item = await db.ReviewNotices.Include(i => i.Review).FirstOrDefaultAsync(i => i.ReviewNoticeId == id);
        if (item is null) return NotFound();
        if (item.Review is null || !CanManage(item.Review)) return Forbid();
        var reviewId = item.ReviewId;
        item.Review.UpdatedAt = DateTime.UtcNow;
        db.ReviewNotices.Remove(item); // Related ratings and comments are removed through cascade delete.
        await db.SaveChangesAsync();
        TempData["Success"] = "Notice, its review-specific risk rating and comments were removed from this review.";
        return RedirectToAction(nameof(Details), new { id = reviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int reviewNoticeId, string commentText, string commentType = "Finding")
    {
        var item = await db.ReviewNotices.Include(i => i.Review).FirstOrDefaultAsync(i => i.ReviewNoticeId == reviewNoticeId);
        if (item is null) return NotFound();
        if (item.Review is null || !CanManage(item.Review)) return Forbid();
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Length > 3000 ||
            !new[] { "Finding", "Internal employee comment", "Internal company note" }.Contains(commentType))
        {
            TempData["Error"] = "Enter a valid comment (up to 3,000 characters) and select a valid comment type.";
            return RedirectToAction(nameof(Details), new { id = item.ReviewId });
        }
        db.ReviewComments.Add(new ReviewComment { ReviewNoticeId = reviewNoticeId, AuthorId = CurrentUserId, CommentType = commentType, CommentText = commentText.Trim() });
        item.Review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = item.ReviewId });
    }

    [HttpGet]
    public async Task<IActionResult> EditComment(int id)
    {
        var comment = await db.ReviewComments.Include(c => c.ReviewNotice).ThenInclude(i => i!.Review).FirstOrDefaultAsync(c => c.CommentId == id);
        if (comment is null) return NotFound();
        if (comment.AuthorId != CurrentUserId || comment.ReviewNotice?.Review is null || !CanManage(comment.ReviewNotice.Review)) return Forbid();
        return View(comment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(int id, string commentText)
    {
        var comment = await db.ReviewComments.Include(c => c.ReviewNotice).ThenInclude(i => i!.Review).FirstOrDefaultAsync(c => c.CommentId == id);
        if (comment is null) return NotFound();
        if (comment.AuthorId != CurrentUserId || comment.ReviewNotice?.Review is null || !CanManage(comment.ReviewNotice.Review)) return Forbid();
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Length > 3000)
        {
            ModelState.AddModelError(nameof(comment.CommentText), "Comment is required and must be 3,000 characters or fewer.");
            return View(comment);
        }
        comment.CommentText = commentText.Trim();
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;
        comment.ReviewNotice.Review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Comment updated. The edited timestamp is visible to readers.";
        return RedirectToAction(nameof(Details), new { id = comment.ReviewNotice.ReviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await db.ReviewComments.Include(c => c.ReviewNotice).ThenInclude(i => i!.Review).FirstOrDefaultAsync(c => c.CommentId == id);
        if (comment is null) return NotFound();
        if (comment.AuthorId != CurrentUserId || comment.ReviewNotice?.Review is null || !CanManage(comment.ReviewNotice.Review)) return Forbid();
        var reviewId = comment.ReviewNotice.ReviewId;
        db.ReviewComments.Remove(comment);
        await db.SaveChangesAsync();
        TempData["Success"] = "Your comment was deleted.";
        return RedirectToAction(nameof(Details), new { id = reviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, bool archived)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == id);
        if (review is null) return NotFound();
        if (!CanManage(review)) return Forbid();
        review.IsArchived = archived;
        review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = archived ? "Review archived." : "Review restored.";
        return RedirectToAction(nameof(Index), new { showArchived = archived });
    }

    private bool CanManage(Review review) => User.IsInRole("Admin") || review.EmployeeId == CurrentUserId;

    private Task<Review?> LoadReviewAsync(int id) => db.Reviews
        .Include(r => r.Employee)
        .Include(r => r.Items).ThenInclude(i => i.Notice)
        .Include(r => r.Items).ThenInclude(i => i.Comments).ThenInclude(c => c.Author)
        .FirstOrDefaultAsync(r => r.ReviewId == id);
}
