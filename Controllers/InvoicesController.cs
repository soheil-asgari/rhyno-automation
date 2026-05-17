using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data; // مطمئن شوید با کانتکست شما یکی است
using OfficeAutomation.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OfficeAutomation.Controllers
{
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // مشاهده فهرست کل فاکتورها
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices.OrderByDescending(i => i.CreatedAt).ToListAsync();
            return View(invoices);
        }

        // دریافت اطلاعات یک فاکتور به صورت JSON برای نمایش در مدال‌ها
        [HttpGet]
        public async Task<IActionResult> GetInvoice(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();
            return Json(new
            {
                id = invoice.Id,
                invoiceNumber = invoice.InvoiceNumber,
                amount = invoice.Amount,
                vendorName = invoice.VendorName,
                invoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice)
        {
            if (!ModelState.IsValid) return BadRequest();

            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim().Replace(" ", "");
            invoice.VendorName = CleanPersianText(invoice.VendorName.Trim());

            bool isDuplicate = await _context.Invoices.AnyAsync(i =>
                i.InvoiceNumber == invoice.InvoiceNumber && i.VendorName == invoice.VendorName);

            if (isDuplicate)
            {
                TempData["ErrorMessage"] = "این شماره فاکتور قبلاً برای این فروشنده ثبت شده است!";
                return RedirectToAction(nameof(Index));
            }

            _context.Add(invoice);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "فاکتور با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Invoice invoice)
        {
            if (id != invoice.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    invoice.InvoiceNumber = invoice.InvoiceNumber.Trim().Replace(" ", "");
                    invoice.VendorName = CleanPersianText(invoice.VendorName.Trim());

                    // بررسی تکراری نبودن بعد از ویرایش
                    bool isDuplicate = await _context.Invoices.AnyAsync(i =>
                        i.InvoiceNumber == invoice.InvoiceNumber &&
                        i.VendorName == invoice.VendorName &&
                        i.Id != id);

                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = "تغییرات به دلیل تکراری بودن شماره فاکتور ثبت نشد.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.Update(invoice);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "فاکتور با موفقیت ویرایش شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.Id)) return NotFound();
                    else throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "فاکتور با موفقیت حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id) => _context.Invoices.Any(e => e.Id == id);

        private string CleanPersianText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("ي", "ی").Replace("ك", "ک");
        }
    }
}