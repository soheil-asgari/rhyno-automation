using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

[Authorize]
public class DepartmentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public DepartmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var departments = await _context.Departments
            .Include(d => d.Manager)
            .Include(d => d.ManagerEmployee)
            .ToListAsync();

        return View(departments);
    }
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewBag.Users = _context.Users.ToList();
        ViewBag.Employees = await _context.HumanCapitalEmployees
            .AsNoTracking()
            .Where(item => item.CurrentStatus == "فعال")
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Department department)
    {
        if (ModelState.IsValid)
        {
            _context.Departments.Add(department);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        ViewBag.Users = _context.Users.ToList();
        ViewBag.Employees = await _context.HumanCapitalEmployees
            .AsNoTracking()
            .Where(item => item.CurrentStatus == "فعال")
            .OrderBy(item => item.FullName)
            .ToListAsync();

        return View(department);
    }





}
