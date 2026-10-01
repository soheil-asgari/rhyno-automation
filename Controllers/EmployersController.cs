using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    [RequiresModule(LicensableModules.HumanCapital)]
public class EmployersController : Controller
    {
        private readonly FinanceDbContext _context;

        // Inventory data is accessed through the IInventoryService application-service
        // interface rather than injecting InventoryDbContext directly. This preserves
        // the module boundary: the Finance/Warehouse controller no longer has a hard
        // compile-time dependency on the Inventory persistence layer (Bمب سوم fix).
        private readonly IInventoryService _inventoryService;

        public EmployersController(FinanceDbContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
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

            // Cross-module check goes through the IInventoryService contract — no direct
            // InventoryDbContext injection needed here.
            var hasIssuances = await _inventoryService.EmployerHasIssuancesAsync(id, cancellationToken);
            var hasInvoices = await _context.Invoices.AnyAsync(item => item.EmployerId == id, cancellationToken);

            if (hasIssuances || hasInvoices)
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
