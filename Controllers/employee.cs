using ANDtr.Data;
using ANDtr.Models;
using ANDtr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ANDtr.Controllers;

[Authorize(Roles = "Employee,Administrator")]
public class  EmployeeController(ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notifications, IReportService reports) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Notices = await db.Notices.CountAsync();
        ViewBag.Reviews = await db.RiskReviews.CountAsync(x => !x.IsArchived);
        ViewBag.High = await db.ReviewNotices.CountAsync(x => x.RiskLevel == "High");
        ViewBag.Unread = await db.ActivityNotifications.CountAsync(x => x.RecipientId == users.GetUserId(User) && !x.IsRead);
        ViewBag.Latest = await db.RiskReviews.Include(x => x.Owner).OrderByDescending(x => x.UpdatedAt).Take(5).ToListAsync();
        return View();
    }

    public async Task<ActionResult> Notices(string? q, string? manufacturer, string? device, string? severity, DateTime? from, DateTime? to)
    {
        var query = db.Notices.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(n => n.Title.Contains(q) || n.Reference.Contains(q) || n.Manufacturer.Contains(q) || n.DeviceType.Contains(q) || n.ProblemType.Contains(q));
        if (!string.IsNullOrWhiteSpace(manufacturer))
            query = query.Where(n => n.Manufacturer == manufacturer);
        if (!string.IsNullOrWhiteSpace(device))
            query = query.Where(n => n.DeviceType == device);
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(n => n.Severity == severity || n.RecallClass == severity);
        if (from.HasValue)
            query = query.Where(n => n.NoticeDate >= from);
        if (to.HasValue)
            query = query.Where(n => n.NoticeDate <= to);
        ViewBag.Q = q;
        ViewBag.Manufacturers = await db.Notices.Select(n => n.Manufacturer).Distinct().ToListAsync();
        ViewBag.Devices = await db.Notices.Select(n => n.DeviceType).Distinct().ToListAsync();
        return View(await query.OrderByDescending(n => n.NoticeDate).ToListAsync());
    }

    public async Task<ActionResult> Notice (int id)
    {
        var n = await db.Notices.FindAsync(id);
        if (n == null) return NotFound();
        ViewBag.Reviews = await db.RiskReviews.Where(r => r.OwnerId == users.GetUserId(User) && !r.IsArchived).ToListAsync();
        return View(n);
    }

    [HttpGet]
    public IActionResult CreateReview() => View();
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> CreateReview(RiskReview model)
    {
        if (!ModelState.IsValid) return View(model);
        model.OwnerId = users.GetUserId(User)!;
        model.CreatedAt = model.UpdatedAt = DateTime.UtcNow;
        db.RiskReviews.Add(model);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Review), new { id = model.Id });
    }

    public async Task<IActionResult> Reviews(string? q, string sort = "newest", bool archived = false)
    {
        var uid = users.GetUserId(User)!;
        var query = db.RiskReviews.Where(r => r.OwnerId == uid && r.IsArchived == archived);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r => r.Title.Contains(q) || r.CustomerOrganization.Contains(q) || r.DeviceContext.Contains(q));
        query = sort == "oldest" ? query.OrderBy(r => r.CreatedAt) : sort == "title" ? query.OrderBy(r => r.Title) : query.OrderByDescending(r => r.UpdatedAt);
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Archived = archived;
        return View(await query.Include(r => r.Notices).ToListAsync());
    }

    public async Task<IActionResult> Review(int id) { var r = await db.RiskReviews.Include(x=>x.Notices).ThenInclude(x => x.Notice)
                                    .Include(x => x.Notices).ThenInclude(x => x.Comments).ThenInclude(x => x.Author)
                                    .Include(x => x.Shares).ThenInclude(x => x.Customer)
                                    .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        if (r.OwnerId != users.GetUserId(User) && !User.IsInRole("Administrator")) return Forbid();
        return View(r);
    }
    [HttpPost,ValidAntiForgeryToken] public async Task<IActionResult> AddNotice(int reviewId, int noticeId) { var r = await OwnedReview(reviewId); 
        if (r == null) return Forbid();
        if (!await db.ReviewNotices.AnyAsync(x => x.RiskReviewId == reviewId && x.NoticeId == noticeId)) db.ReviewNotices.Add(new ReviewNotice { RiskReviewId = reviewId, NoticeId = noticeId }); r.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return RedirectToAction(nameof(Review), new { id = reviewId }); }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> SetRisk(int id, string risk) { var item = await db.ReviewNotices.Include(x => x.RiskReview).FirstOrDefaultAsync(x => x.Id == id); if (item == null || !await CanEdit(item.RiskReview!)) return Forbid(); if (!new[] { "Low", "Medium", "High" }.Contains(risk)) return BadRequest(); item.RiskLevel = risk; item.RiskReview!.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return RedirectToAction(nameof(Review), new { id = item.RiskReviewId }); }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> RemoveNotice(int id) { var r = await db.ReviewNotices.Include(x => x.RiskReview).FirstOrDefaultAsync(x => x.Id == id); if (r == null ||!await CanEdit(r.RiskReview!)) return Forbid(); r.IsArchived = true; r.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return RedirectToAction(nameof(Reviews)); }