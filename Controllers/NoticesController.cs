using ANDtrSafetyMvc.Data;
using ANDtrSafetyMvc.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ANDtrSafetyMvc.Controllers;

[Authorize(Policy = "Employees")]
public class NoticesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(NoticeSearchViewModel filter)
    {
        var query = db.Notices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.DeviceName.Contains(term) || n.Manufacturer.Contains(term) || n.ProblemType.Contains(term) || n.NoticeNumber.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Manufacturer)) query = query.Where(n => n.Manufacturer == filter.Manufacturer);
        if (!string.IsNullOrWhiteSpace(filter.DeviceType)) query = query.Where(n => n.DeviceType == filter.DeviceType);
        if (!string.IsNullOrWhiteSpace(filter.SeverityClass)) query = query.Where(n => n.SeverityClass == filter.SeverityClass);
        if (filter.DateFrom.HasValue) query = query.Where(n => n.DatePublished >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue) query = query.Where(n => n.DatePublished <= filter.DateTo.Value);

        filter.Results = await query.OrderByDescending(n => n.DatePublished).Take(200).ToListAsync();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("Admin");
        ViewBag.Reviews = await db.Reviews.Where(r => (isAdmin || r.EmployeeId == userId) && !r.IsArchived).OrderByDescending(r => r.UpdatedAt).ToListAsync();
        filter.Manufacturers = await db.Notices.Select(n => n.Manufacturer).Distinct().OrderBy(x => x).ToListAsync();
        filter.DeviceTypes = await db.Notices.Select(n => n.DeviceType).Distinct().OrderBy(x => x).ToListAsync();
        filter.SeverityClasses = await db.Notices.Select(n => n.SeverityClass).Distinct().OrderBy(x => x).ToListAsync();
        return View(filter);
    }

    public async Task<IActionResult> Details(int id)
    {
        var notice = await db.Notices.AsNoTracking().FirstOrDefaultAsync(n => n.NoticeId == id);
        return notice is null ? NotFound() : View(notice);
    }
}
