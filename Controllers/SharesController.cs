using ANDtrSafetyMvc.Data;
using ANDtrSafetyMvc.Models;
using ANDtrSafetyMvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ANDtrSafetyMvc.Controllers;

[Authorize(Policy = "Employees")]
public class SharesController(AppDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Share(int reviewId)
    {
        var review = await db.Reviews.Include(r => r.Shares).ThenInclude(s => s.Customer).FirstOrDefaultAsync(r => r.ReviewId == reviewId);
        if (review is null) return NotFound();
        if (!User.IsInRole("Admin") && review.EmployeeId != CurrentUserId) return Forbid();
        return View(new ShareReviewViewModel { Review = review, Customers = (await users.GetUsersInRoleAsync("Customer")).OrderBy(u => u.FullName).ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(ShareReviewViewModel model)
    {
        var review = await db.Reviews.Include(r => r.Shares).ThenInclude(s => s.Customer).FirstOrDefaultAsync(r => r.ReviewId == model.Review.ReviewId);
        if (review is null) return NotFound();
        if (!User.IsInRole("Admin") && review.EmployeeId != CurrentUserId) return Forbid();
        var customer = await users.FindByIdAsync(model.CustomerId);
        if (customer is null || !await users.IsInRoleAsync(customer, "Customer"))
        {
            ModelState.AddModelError(nameof(model.CustomerId), "Select a valid customer account.");
            return await ReturnShareView(review, model);
        }

        var existing = review.Shares.FirstOrDefault(s => s.CustomerId == customer.Id);
        if (existing is null)
        {
            existing = new ReviewShare { ReviewId = review.ReviewId, CustomerId = customer.Id, SharedById = CurrentUserId };
            db.ReviewShares.Add(existing);
        }
        existing.IsActive = true;
        existing.RevokedAt = null;
        existing.SharedById = CurrentUserId;
        existing.SharedAt = DateTime.UtcNow;
        existing.IncludeNoticeDetails = model.IncludeNoticeDetails;
        existing.IncludeRiskLevels = model.IncludeRiskLevels;
        existing.IncludeFindings = model.IncludeFindings;
        existing.IncludeInternalEmployeeComments = model.IncludeInternalEmployeeComments;
        existing.IncludeInternalCompanyNotes = model.IncludeInternalCompanyNotes;
        existing.IncludeRecommendations = model.IncludeRecommendations;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Review shared with {customer.FullName}.";
        return RedirectToAction(nameof(Share), new { reviewId = review.ReviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var share = await db.ReviewShares.Include(s => s.Review).Include(s => s.Customer).FirstOrDefaultAsync(s => s.ShareId == id);
        if (share is null) return NotFound();
        if (share.Review is null || (!User.IsInRole("Admin") && share.Review.EmployeeId != CurrentUserId)) return Forbid();
        share.IsActive = false;
        share.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Access revoked for {share.Customer?.FullName ?? "the customer"}.";
        return RedirectToAction(nameof(Share), new { reviewId = share.ReviewId });
    }

    private async Task<IActionResult> ReturnShareView(Review review, ShareReviewViewModel model)
    {
        model.Review = review;
        model.Customers = (await users.GetUsersInRoleAsync("Customer")).OrderBy(u => u.FullName).ToList();
        return View("Share", model);
    }
}
