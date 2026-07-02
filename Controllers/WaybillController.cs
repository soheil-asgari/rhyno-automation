using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    public class WaybillController : Controller
    {
        private readonly FinanceDbContext _context;
        private static readonly string[] DefaultPaymentStatuses = ["Paid", "Pending", "Internal"];
        private static readonly string[] DefaultVehicleTypes = ["تریلی", "خاور", "کامیون"];

        public WaybillController(FinanceDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(WaybillIndexVM filter)
        {
            var query = _context.Waybills
                .AsNoTracking()
                .Where(item => !item.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.Trim();
                query = query.Where(item =>
                    item.WaybillNumber.Contains(searchTerm) ||
                    item.DriverName.Contains(searchTerm) ||
                    item.OriginCity.Contains(searchTerm) ||
                    item.DestinationCity.Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                var paymentStatus = filter.PaymentStatus.Trim();
                query = query.Where(item => item.PaymentStatus == paymentStatus);
            }

            filter.TotalCount = await _context.Waybills
                .AsNoTracking()
                .CountAsync(item => !item.IsDeleted);

            filter.FilteredCount = await query.CountAsync();
            filter.Items = await query
                .OrderByDescending(item => item.IssueDate)
                .ThenByDescending(item => item.Id)
                .Select(item => new WaybillListItemVM
                {
                    Id = item.Id,
                    WaybillNumber = item.WaybillNumber,
                    IssueDate = item.IssueDate,
                    OriginCity = item.OriginCity,
                    DestinationCity = item.DestinationCity,
                    DriverName = item.DriverName,
                    NetPayToDriver = item.NetPayToDriver,
                    PaymentStatus = item.PaymentStatus
                })
                .ToListAsync();

            filter.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();

            return View(filter);
        }

        public async Task<IActionResult> Create()
        {
            var model = new WaybillCreateVM
            {
                IssueDate = DateTime.Today,
                LoadingDate = DateTime.Today,
                PaymentStatus = "Pending",
                AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync(),
                AvailableVehicleTypes = DefaultVehicleTypes.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WaybillCreateVM model)
        {
            ValidateWaybillFinancials(model.TotalFreightCharges, model.DriverCommission, model.NetPayToDriver, model.IssueDate, model.LoadingDate);

            if (!ModelState.IsValid)
            {
                model.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();
                model.AvailableVehicleTypes = DefaultVehicleTypes.ToList();
                return View(model);
            }

            var duplicateExists = await _context.Waybills
                .AsNoTracking()
                .AnyAsync(item => !item.IsDeleted && item.WaybillNumber == model.WaybillNumber.Trim());

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(model.WaybillNumber), "این شماره بارنامه قبلا ثبت شده است.");
                model.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();
                model.AvailableVehicleTypes = DefaultVehicleTypes.ToList();
                return View(model);
            }

            var entity = new Waybill
            {
                WaybillNumber = model.WaybillNumber.Trim(),
                IssueDate = model.IssueDate,
                LoadingDate = model.LoadingDate,
                SenderName = model.SenderName.Trim(),
                OriginCity = model.OriginCity.Trim(),
                ReceiverName = model.ReceiverName.Trim(),
                DestinationCity = model.DestinationCity.Trim(),
                DriverName = model.DriverName.Trim(),
                DriverNationalId = model.DriverNationalId.Trim(),
                DriverPhone = model.DriverPhone.Trim(),
                VehiclePlateNumber = model.VehiclePlateNumber.Trim(),
                VehicleType = model.VehicleType.Trim(),
                CargoType = model.CargoType.Trim(),
                Weight = model.Weight,
                TotalFreightCharges = model.TotalFreightCharges,
                DriverCommission = model.DriverCommission,
                NetPayToDriver = model.NetPayToDriver,
                PaymentStatus = model.PaymentStatus.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Waybills.Add(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Waybills
                .AsNoTracking()
                .Where(item => item.Id == id.Value && !item.IsDeleted)
                .Select(item => new WaybillDetailsVM
                {
                    Id = item.Id,
                    WaybillNumber = item.WaybillNumber,
                    IssueDate = item.IssueDate,
                    LoadingDate = item.LoadingDate,
                    SenderName = item.SenderName,
                    OriginCity = item.OriginCity,
                    ReceiverName = item.ReceiverName,
                    DestinationCity = item.DestinationCity,
                    DriverName = item.DriverName,
                    DriverNationalId = item.DriverNationalId,
                    DriverPhone = item.DriverPhone,
                    VehiclePlateNumber = item.VehiclePlateNumber,
                    VehicleType = item.VehicleType,
                    CargoType = item.CargoType,
                    Weight = item.Weight,
                    TotalFreightCharges = item.TotalFreightCharges,
                    DriverCommission = item.DriverCommission,
                    NetPayToDriver = item.NetPayToDriver,
                    PaymentStatus = item.PaymentStatus,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Waybills
                .AsNoTracking()
                .Where(item => item.Id == id.Value && !item.IsDeleted)
                .Select(item => new WaybillEditVM
                {
                    Id = item.Id,
                    WaybillNumber = item.WaybillNumber,
                    IssueDate = item.IssueDate,
                    LoadingDate = item.LoadingDate,
                    SenderName = item.SenderName,
                    OriginCity = item.OriginCity,
                    ReceiverName = item.ReceiverName,
                    DestinationCity = item.DestinationCity,
                    DriverName = item.DriverName,
                    DriverNationalId = item.DriverNationalId,
                    DriverPhone = item.DriverPhone,
                    VehiclePlateNumber = item.VehiclePlateNumber,
                    VehicleType = item.VehicleType,
                    CargoType = item.CargoType,
                    Weight = item.Weight,
                    TotalFreightCharges = item.TotalFreightCharges,
                    DriverCommission = item.DriverCommission,
                    NetPayToDriver = item.NetPayToDriver,
                    PaymentStatus = item.PaymentStatus
                })
                .FirstOrDefaultAsync();

            if (model == null)
            {
                return NotFound();
            }

            model.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();
            model.AvailableVehicleTypes = DefaultVehicleTypes.ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WaybillEditVM model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ValidateWaybillFinancials(model.TotalFreightCharges, model.DriverCommission, model.NetPayToDriver, model.IssueDate, model.LoadingDate);

            if (!ModelState.IsValid)
            {
                model.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();
                model.AvailableVehicleTypes = DefaultVehicleTypes.ToList();
                return View(model);
            }

            var duplicateExists = await _context.Waybills
                .AsNoTracking()
                .AnyAsync(item => !item.IsDeleted && item.WaybillNumber == model.WaybillNumber.Trim() && item.Id != id);

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(model.WaybillNumber), "این شماره بارنامه قبلا ثبت شده است.");
                model.AvailablePaymentStatuses = await GetAvailablePaymentStatusesAsync();
                model.AvailableVehicleTypes = DefaultVehicleTypes.ToList();
                return View(model);
            }

            var waybill = await _context.Waybills
                .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);

            if (waybill == null)
            {
                return NotFound();
            }

            waybill.WaybillNumber = model.WaybillNumber.Trim();
            waybill.IssueDate = model.IssueDate;
            waybill.LoadingDate = model.LoadingDate;
            waybill.SenderName = model.SenderName.Trim();
            waybill.OriginCity = model.OriginCity.Trim();
            waybill.ReceiverName = model.ReceiverName.Trim();
            waybill.DestinationCity = model.DestinationCity.Trim();
            waybill.DriverName = model.DriverName.Trim();
            waybill.DriverNationalId = model.DriverNationalId.Trim();
            waybill.DriverPhone = model.DriverPhone.Trim();
            waybill.VehiclePlateNumber = model.VehiclePlateNumber.Trim();
            waybill.VehicleType = model.VehicleType.Trim();
            waybill.CargoType = model.CargoType.Trim();
            waybill.Weight = model.Weight;
            waybill.TotalFreightCharges = model.TotalFreightCharges;
            waybill.DriverCommission = model.DriverCommission;
            waybill.NetPayToDriver = model.NetPayToDriver;
            waybill.PaymentStatus = model.PaymentStatus.Trim();
            waybill.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Waybills
                .AsNoTracking()
                .Where(item => item.Id == id.Value && !item.IsDeleted)
                .Select(item => new WaybillDetailsVM
                {
                    Id = item.Id,
                    WaybillNumber = item.WaybillNumber,
                    IssueDate = item.IssueDate,
                    LoadingDate = item.LoadingDate,
                    SenderName = item.SenderName,
                    OriginCity = item.OriginCity,
                    ReceiverName = item.ReceiverName,
                    DestinationCity = item.DestinationCity,
                    DriverName = item.DriverName,
                    DriverNationalId = item.DriverNationalId,
                    DriverPhone = item.DriverPhone,
                    VehiclePlateNumber = item.VehiclePlateNumber,
                    VehicleType = item.VehicleType,
                    CargoType = item.CargoType,
                    Weight = item.Weight,
                    TotalFreightCharges = item.TotalFreightCharges,
                    DriverCommission = item.DriverCommission,
                    NetPayToDriver = item.NetPayToDriver,
                    PaymentStatus = item.PaymentStatus,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var waybill = await _context.Waybills
                .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);

            if (waybill != null)
            {
                waybill.IsDeleted = true;
                waybill.DeletedAt = DateTime.Now;
                waybill.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<string>> GetAvailablePaymentStatusesAsync()
        {
            var statuses = await _context.Waybills
                .AsNoTracking()
                .Where(item => !item.IsDeleted && !string.IsNullOrWhiteSpace(item.PaymentStatus))
                .Select(item => item.PaymentStatus)
                .Distinct()
                .ToListAsync();

            return DefaultPaymentStatuses
                .Concat(statuses)
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(status => status)
                .ToList();
        }

        private void ValidateWaybillFinancials(decimal totalFreight, decimal commission, decimal netPay, DateTime issueDate, DateTime loadingDate)
        {
            if (commission > totalFreight)
            {
                ModelState.AddModelError("DriverCommission", "کمیسیون راننده نمی‌تواند بیشتر از مبلغ کل کرایه باشد.");
            }

            if (netPay > totalFreight)
            {
                ModelState.AddModelError("NetPayToDriver", "صافی دریافتی راننده نمی‌تواند بیشتر از مبلغ کل کرایه باشد.");
            }

            if (loadingDate < issueDate)
            {
                ModelState.AddModelError("LoadingDate", "تاریخ بارگیری نمی‌تواند قبل از تاریخ صدور باشد.");
            }
        }
    }
}
