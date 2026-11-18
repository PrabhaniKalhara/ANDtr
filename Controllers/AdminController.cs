using ANDtrSafetyMvc.Data;
using ANDtrSafetyMvc.Models;
using ANDtrSafetyMvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ANDtrSafetyMvc.Controllers;

[Authorize(Policy = "Administrators")]
public class AdminController(UserManager<ApplicationUser> users, AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var allUsers = await db.Users.OrderBy(u => u.FullName).ToListAsync();
        var results = new List<AdminUserRowViewModel>();
        foreach (var user in allUsers)
        {
            var roles = await users.GetRolesAsync(user);
            results.Add(new AdminUserRowViewModel { User = user, Role = roles.FirstOrDefault() ?? "No role" });
        }
        return View(results);
    }

    [HttpGet]
    public IActionResult CreateUser() => View(new CreateUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!new[] { "Employee", "Customer", "Admin" }.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            Organization = model.Organization,
            Department = model.Department,
            EmployeeCode = model.EmployeeCode
        };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(user, model.Role);
            TempData["Success"] = $"{model.Role} account created for {user.FullName}.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId, string role)
    {
        if (!new[] { "Employee", "Customer", "Admin" }.Contains(role)) return BadRequest("Invalid role.");
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        var currentId = users.GetUserId(User);
        if (user.Id == currentId && role != "Admin")
        {
            TempData["Error"] = "You cannot remove your own administrator role.";
            return RedirectToAction(nameof(Index));
        }
        var existingRoles = await users.GetRolesAsync(user);
        var remove = await users.RemoveFromRolesAsync(user, existingRoles);
        if (!remove.Succeeded)
        {
            TempData["Error"] = string.Join("; ", remove.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }
        var add = await users.AddToRoleAsync(user, role);
        TempData[add.Succeeded ? "Success" : "Error"] = add.Succeeded
            ? $"{user.FullName}'s role was changed to {role}."
            : string.Join("; ", add.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Index));
    }

}
