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
    public class VendorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VendorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm, CancellationToken cancellationToken)
        {
            var query = _context.Vendors.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item =>
                    item.Name.Contains(term) ||
                    (item.EconomicCode ?? string.Empty).Contains(term) ||
                    (item.NationalId ?? string.Empty).Contains(term));
            }

            var model = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
            ViewBag.SearchTerm = searchTerm;
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Vendor { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vendor model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Name = model.Name.Trim();
            model.EconomicCode = model.EconomicCode?.Trim();
            model.NationalId = model.NationalId?.Trim();
            model.Phone = model.Phone?.Trim();
            model.Address = model.Address?.Trim();

            _context.Vendors.Add(model);
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Vendor model, CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Name = model.Name.Trim();
            entity.EconomicCode = model.EconomicCode?.Trim();
            entity.NationalId = model.NationalId?.Trim();
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
            var entity = await _context.Vendors.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var hasReferences = await _context.WarehouseReceipts.AnyAsync(item => item.VendorId == id, cancellationToken);
            if (hasReferences)
            {
                entity.IsActive = false;
                await _context.SaveChangesAsync(cancellationToken);
                TempData["VendorMessage"] = "به دلیل وجود گردش، تامین‌کننده غیرفعال شد.";
                return RedirectToAction(nameof(Index));
            }

            _context.Vendors.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
