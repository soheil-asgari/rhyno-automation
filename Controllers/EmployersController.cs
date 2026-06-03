using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    public class EmployersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm, CancellationToken cancellationToken)
        {
            var query = _context.Employers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item =>
                    item.Name.Contains(term) ||
                    (item.ContractNumber ?? string.Empty).Contains(term) ||
                    (item.Phone ?? string.Empty).Contains(term));
            }

            var model = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
            ViewBag.SearchTerm = searchTerm;
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Employer { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employer model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Name = model.Name.Trim();
            model.ContractNumber = model.ContractNumber?.Trim();
            model.Phone = model.Phone?.Trim();
            model.Address = model.Address?.Trim();

            _context.Employers.Add(model);
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Employers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employer model, CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = await _context.Employers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Name = model.Name.Trim();
            entity.ContractNumber = model.ContractNumber?.Trim();
            entity.Phone = model.Phone?.Trim();
            entity.Address = model.Address?.Trim();
            entity.IsActive = model.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Employers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var hasReferences =
                await _context.WarehouseIssuances.AnyAsync(item => item.EmployerId == id, cancellationToken) ||
                await _context.Invoices.AnyAsync(item => item.EmployerId == id, cancellationToken);
            if (hasReferences)
            {
                entity.IsActive = false;
                await _context.SaveChangesAsync(cancellationToken);
                TempData["EmployerMessage"] = "به دلیل وجود گردش، کارفرما غیرفعال شد.";
                return RedirectToAction(nameof(Index));
            }

            _context.Employers.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
