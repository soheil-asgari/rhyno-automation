using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Commercial.Domain;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Modules.Commercial.Application;

/// <summary>
/// Orchestrates the Commercial module (sales, purchases, returns, quotations, parties,
/// settlements and financial attachments). All accounting postings are delegated to the
/// shared Phase-1 accounting engine (<see cref="FinanceLedgerService"/>) — no accounting
/// logic is duplicated here.
/// </summary>
public sealed class CommercialService
{
    private readonly FinanceDbContext _context;
    private readonly FinanceLedgerService _ledgerService;

    public CommercialService(FinanceDbContext context, FinanceLedgerService ledgerService)
    {
        _context = context;
        _ledgerService = ledgerService;
    }

    // ── Sales / Purchases master grids ────────────────────────────────────────

    public async Task<CommercialInvoiceIndexVM> BuildInvoiceIndexAsync(
        string invoiceType,
        string? searchTerm,
        int? year,
        string? partyNameFilter,
        string? statusFilter,
        string? paymentStatusFilter,
        CancellationToken cancellationToken)
    {
        var normalizedType = invoiceType == "Purchase" ? "Purchase" : "Sale";
        var filtered = _context.Invoices.AsNoTracking().AsQueryable()
            .Where(item => item.InvoiceType == normalizedType);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = PersianTextNormalizer.Normalize(searchTerm);
            filtered = filtered.Where(item => item.InvoiceNumber.Contains(term) || item.PartyName.Contains(term));
        }

        if (year.HasValue && year.Value > 1300)
        {
            var yearToken = year.Value.ToString(CultureInfo.InvariantCulture);
            filtered = filtered.Where(item => item.DateShamsi.StartsWith(yearToken));
        }

        if (!string.IsNullOrWhiteSpace(partyNameFilter))
        {
            var party = PersianTextNormalizer.Normalize(partyNameFilter);
            filtered = filtered.Where(item => item.PartyName.Contains(party));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
        {
            filtered = filtered.Where(item => item.WorkflowStatus == statusFilter);
        }

        var invoices = await filtered
            .OrderByDescending(item => item.InvoiceDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).ToList();

        var settlementGroups = await _context.SettlementTransactions
            .AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId) && item.Status == WorkflowStatus.Approved)
            .GroupBy(item => item.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Total = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var returnGroups = await _context.InvoiceReturns
            .AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId) && item.Status == WorkflowStatus.Approved)
            .GroupBy(item => item.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Total = group.Sum(item => item.GrandTotal) })
            .ToListAsync(cancellationToken);

        var attachmentCounts = await _context.InvoiceAttachments
            .AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var voucherNumbers = await _context.VoucherHeaders
            .AsNoTracking()
            .Where(item => item.DocumentNumber != null &&
                           (item.DocumentNumber.StartsWith("SAL-INV-") || item.DocumentNumber.StartsWith("PUR-INV-")))
            .Select(item => new { item.DocumentNumber, item.VoucherNumber })
            .ToListAsync(cancellationToken);

        var rowItems = new List<CommercialInvoiceRowVM>();
        foreach (var invoice in invoices)
        {
            var paid = settlementGroups.FirstOrDefault(item => item.InvoiceId == invoice.Id)?.Total ?? 0m;
            var returned = returnGroups.FirstOrDefault(item => item.InvoiceId == invoice.Id)?.Total ?? 0m;
            var outstanding = invoice.GrandTotal - returned - paid;
            var cancelled = WorkflowStatus.Normalize(invoice.WorkflowStatus) == WorkflowStatus.Canceled;
            var paymentStatus = CommercialPaymentStatus.Resolve(invoice.GrandTotal, paid, returned, cancelled);
            var documentNumber = (invoice.InvoiceType == "Purchase" ? "PUR-INV-" : "SAL-INV-") + invoice.Id;

            rowItems.Add(new CommercialInvoiceRowVM
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceType = invoice.InvoiceType,
                DateShamsi = invoice.DateShamsi,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                DueDateShamsi = ToShamsi(invoice.DueDate),
                PartyName = invoice.PartyName,
                SubTotal = invoice.SubTotal,
                VatAmount = invoice.VatAmount,
                GrandTotal = invoice.GrandTotal,
                PaidAmount = paid,
                ReturnedAmount = returned,
                OutstandingAmount = outstanding,
                VoucherNumber = voucherNumbers.FirstOrDefault(item => item.DocumentNumber == documentNumber)?.VoucherNumber?.ToString(),
                WorkflowStatus = invoice.WorkflowStatus,
                PaymentStatus = paymentStatus,
                AttachmentCount = attachmentCounts.FirstOrDefault(item => item.InvoiceId == invoice.Id)?.Count ?? 0
            });
        }

        if (!string.IsNullOrWhiteSpace(paymentStatusFilter) && paymentStatusFilter != "All")
        {
            rowItems = rowItems.Where(item => item.PaymentStatus == paymentStatusFilter).ToList();
        }

        return new CommercialInvoiceIndexVM
        {
            SearchTerm = searchTerm,
            Year = year,
            PartyNameFilter = partyNameFilter,
            StatusFilter = statusFilter,
            PaymentStatusFilter = paymentStatusFilter,
            InvoiceType = normalizedType,
            Items = rowItems,
            TotalPayable = rowItems.Sum(item => item.GrandTotal),
            TotalPaid = rowItems.Sum(item => item.PaidAmount),
            TotalOutstanding = rowItems.Sum(item => item.OutstandingAmount)
        };
    }

    // ── Invoice detail (settlements + attachments + accounting trace) ────────

    public async Task<InvoiceDetailVM?> BuildInvoiceDetailAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice == null)
        {
            return null;
        }

        var settlements = await _context.SettlementTransactions.AsNoTracking()
            .Where(item => item.InvoiceId == id && item.Status == WorkflowStatus.Approved)
            .OrderByDescending(item => item.SettlementDate)
            .ToListAsync(cancellationToken);

        var attachments = await _context.InvoiceAttachments.AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .OrderByDescending(item => item.UploadedAt)
            .ToListAsync(cancellationToken);

        var returns = await _context.InvoiceReturns.AsNoTracking()
            .Where(item => item.InvoiceId == id && item.Status == WorkflowStatus.Approved)
            .ToListAsync(cancellationToken);

        var documentNumber = (invoice.InvoiceType == "Purchase" ? "PUR-INV-" : "SAL-INV-") + invoice.Id;
        var voucher = await _context.VoucherHeaders.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);

        var paid = settlements.Sum(item => item.Amount);
        var returned = returns.Sum(item => item.GrandTotal);
        var outstanding = invoice.GrandTotal - returned - paid;
        var cancelled = WorkflowStatus.Normalize(invoice.WorkflowStatus) == WorkflowStatus.Canceled;

        return new InvoiceDetailVM
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceType = invoice.InvoiceType,
            PartyName = invoice.PartyName,
            DateShamsi = invoice.DateShamsi,
            DueDateShamsi = ToShamsi(invoice.DueDate),
            SubTotal = invoice.SubTotal,
            VatAmount = invoice.VatAmount,
            GrandTotal = invoice.GrandTotal,
            Notes = invoice.Notes,
            WorkflowStatus = invoice.WorkflowStatus,
            PaymentStatus = CommercialPaymentStatus.Resolve(invoice.GrandTotal, paid, returned, cancelled),
            PaidAmount = paid,
            ReturnedAmount = returned,
            OutstandingAmount = outstanding,
            VoucherNumber = voucher?.VoucherNumber?.ToString(),
            Settlements = settlements.Select(item => new SettlementRowVM
            {
                Id = item.Id,
                ReferenceNumber = item.ReferenceNumber,
                DateShamsi = item.DateShamsi,
                Amount = item.Amount,
                PaymentMethod = item.PaymentMethod,
                Status = item.Status,
                VoucherNumber = item.VoucherDocumentNumber
            }).ToList(),
            Attachments = attachments.Select(item => new InvoiceAttachmentVM
            {
                Id = item.Id,
                FileName = item.FileName,
                Category = item.Category,
                CategoryLabel = InvoiceAttachmentCategoryLabels.Label(item.Category),
                ContentType = item.ContentType,
                SizeBytes = item.SizeBytes,
                Description = item.Description,
                UploadedAt = item.UploadedAt
            }).ToList()
        };
    }

    // ── Parties ───────────────────────────────────────────────────────────────

    public async Task<List<PartyRowVM>> GetPartiesAsync(string? searchTerm, CancellationToken cancellationToken)
    {
        var query = _context.CommercialParties.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = PersianTextNormalizer.Normalize(searchTerm);
            query = query.Where(item => item.Name.Contains(term) || (item.EconomicCode != null && item.EconomicCode.Contains(term)));
        }

        var parties = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var partyIds = parties.Select(item => item.Id).ToList();

        var balances = await _context.Invoices.AsNoTracking()
            .Where(item => item.CommercialPartyId.HasValue && partyIds.Contains(item.CommercialPartyId!.Value))
            .Select(item => new { PartyId = item.CommercialPartyId!.Value, item.InvoiceType, item.GrandTotal, item.WorkflowStatus })
            .ToListAsync(cancellationToken);

        return parties.Select(party =>
        {
            var partyInvoices = balances.Where(item => item.PartyId == party.Id).ToList();
            var receivable = partyInvoices
                .Where(item => item.InvoiceType == "Sale" && item.WorkflowStatus != WorkflowStatus.Canceled)
                .Sum(item => item.GrandTotal);
            var payable = partyInvoices
                .Where(item => item.InvoiceType == "Purchase" && item.WorkflowStatus != WorkflowStatus.Canceled)
                .Sum(item => item.GrandTotal);
            return new PartyRowVM
            {
                Id = party.Id,
                Name = party.Name,
                PartyType = party.PartyType,
                PartyTypeLabel = party.PartyType switch
                {
                    CommercialPartyType.Customer => "مشتری",
                    CommercialPartyType.Supplier => "تامین‌کننده",
                    _ => "مشتری/تامین‌کننده"
                },
                EconomicCode = party.EconomicCode,
                NationalCode = party.NationalCode,
                Phone = party.Phone,
                Email = party.Email,
                IsActive = party.IsActive,
                Balance = party.PartyType == CommercialPartyType.Supplier ? payable : receivable
            };
        }).ToList();
    }

    public async Task<PartyUpsertVM?> GetPartyForEditAsync(int id, CancellationToken cancellationToken)
    {
        var party = await _context.CommercialParties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (party == null)
        {
            return null;
        }

        return new PartyUpsertVM
        {
            Id = party.Id,
            Name = party.Name,
            PartyType = party.PartyType,
            NationalCode = party.NationalCode,
            EconomicCode = party.EconomicCode,
            Phone = party.Phone,
            Email = party.Email,
            Address = party.Address,
            Notes = party.Notes,
            IsActive = party.IsActive
        };
    }

    public async Task<(CommercialParty Party, bool IsNew)> SavePartyAsync(PartyUpsertVM model, string? userId, CancellationToken cancellationToken)
    {
        if (model.Id > 0)
        {
            var party = await _context.CommercialParties.FirstOrDefaultAsync(item => item.Id == model.Id, cancellationToken)
                ?? throw new InvalidOperationException("طرف حساب یافت نشد.");
            party.Name = model.Name.Trim();
            party.PartyType = model.PartyType;
            party.NationalCode = model.NationalCode?.Trim();
            party.EconomicCode = model.EconomicCode?.Trim();
            party.Phone = model.Phone?.Trim();
            party.Email = model.Email?.Trim();
            party.Address = model.Address?.Trim();
            party.Notes = model.Notes?.Trim();
            party.IsActive = model.IsActive;
            party.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return (party, false);
        }

        var newParty = new CommercialParty
        {
            Name = model.Name.Trim(),
            PartyType = model.PartyType,
            NationalCode = model.NationalCode?.Trim(),
            EconomicCode = model.EconomicCode?.Trim(),
            Phone = model.Phone?.Trim(),
            Email = model.Email?.Trim(),
            Address = model.Address?.Trim(),
            Notes = model.Notes?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.Now,
            CreatedByUserId = userId
        };
        _context.CommercialParties.Add(newParty);
        await _context.SaveChangesAsync(cancellationToken);
        return (newParty, true);
    }

    // ── Invoice posting / cancellation ────────────────────────────────────────

    public async Task<VoucherHeader> PostInvoiceAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceWithVoucherAsync(id, cancellationToken);
        invoice.WorkflowStatus = WorkflowStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);
        var voucher = await _ledgerService.PostInvoiceAsync(invoice, cancellationToken);
        invoice.WorkflowStatus = WorkflowStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    public async Task<VoucherHeader> CancelInvoiceAsync(int id, string? reason, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceWithVoucherAsync(id, cancellationToken);
        if (WorkflowStatus.Normalize(invoice.WorkflowStatus) == WorkflowStatus.Canceled)
        {
            throw new InvalidOperationException("فاکتور قبلاً ابطال شده است.");
        }

        var documentNumber = (invoice.InvoiceType == "Purchase" ? "PUR-INV-" : "SAL-INV-") + invoice.Id;
        var voucher = await _context.VoucherHeaders.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);

        if (voucher != null)
        {
            await _ledgerService.ReverseVoucherAsync(voucher.Id, reason ?? "Commercial invoice cancellation", cancellationToken: cancellationToken);
        }

        invoice.WorkflowStatus = WorkflowStatus.Canceled;
        await _context.SaveChangesAsync(cancellationToken);
        return voucher!;
    }

    // ── Returns ───────────────────────────────────────────────────────────────

    public async Task<InvoiceReturnIndexVM> BuildReturnIndexAsync(string returnType, string? searchTerm, CancellationToken cancellationToken)
    {
        var normalized = returnType == "Purchase" ? "Purchase" : "Sale";
        var query = _context.InvoiceReturns.AsNoTracking()
            .Include(item => item.Invoice)
            .Where(item => item.ReturnType == normalized)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = PersianTextNormalizer.Normalize(searchTerm);
            query = query.Where(item => item.ReturnNumber.Contains(term) || (item.Invoice != null && item.Invoice.InvoiceNumber.Contains(term)));
        }

        var items = await query.OrderByDescending(item => item.ReturnDate).ThenByDescending(item => item.Id)
            .Select(item => new InvoiceReturnRowVM
            {
                Id = item.Id,
                ReturnNumber = item.ReturnNumber,
                ReturnType = item.ReturnType,
                InvoiceNumber = item.Invoice != null ? item.Invoice.InvoiceNumber : "-",
                PartyName = item.Invoice != null ? item.Invoice.PartyName : (item.CommercialParty != null ? item.CommercialParty.Name : "-"),
                DateShamsi = item.DateShamsi,
                SubTotal = item.SubTotal,
                VatAmount = item.VatAmount,
                GrandTotal = item.GrandTotal,
                Status = item.Status,
                VoucherNumber = item.VoucherDocumentNumber,
                Reason = item.Reason
            })
            .ToListAsync(cancellationToken);

        return new InvoiceReturnIndexVM { ReturnType = normalized, SearchTerm = searchTerm, Items = items };
    }

    public async Task<InvoiceReturn> CreateReturnAsync(
        int invoiceId,
        string? reason,
        string dateShamsi,
        DateTime returnDate,
        IEnumerable<InvoiceReturnItem> items,
        string? userId,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("فاکتور مبدأ یافت نشد.");

        var validItems = items.Where(item => !string.IsNullOrWhiteSpace(item.ItemName) && item.Quantity > 0).ToList();
        if (validItems.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک ردیف معتبر برای برگشت وارد کنید.");
        }

        var returnEntity = new InvoiceReturn
        {
            ReturnType = invoice.InvoiceType,
            ReturnNumber = await BuildNextReturnNumberAsync(invoice.InvoiceType, cancellationToken),
            InvoiceId = invoice.Id,
            CommercialPartyId = invoice.CommercialPartyId,
            Reason = reason?.Trim(),
            ReturnDate = returnDate.Date,
            DateShamsi = string.IsNullOrWhiteSpace(dateShamsi) ? FinancialAppServiceProxy.GetTodayShamsi() : PersianTextNormalizer.Normalize(dateShamsi),
            Status = WorkflowStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now,
            SubTotal = validItems.Sum(item => item.LineSubTotal),
            VatAmount = validItems.Sum(item => item.LineVatAmount),
            GrandTotal = validItems.Sum(item => item.LineGrandTotal),
            Items = validItems.Select(item => new InvoiceReturnItem
            {
                ProductId = item.ProductId,
                ItemName = item.ItemName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineSubTotal = item.LineSubTotal,
                LineVatAmount = item.LineVatAmount,
                LineGrandTotal = item.LineGrandTotal
            }).ToList()
        };

        _context.InvoiceReturns.Add(returnEntity);
        await _context.SaveChangesAsync(cancellationToken);
        return returnEntity;
    }

    public async Task<VoucherHeader> FinalizeReturnAsync(int returnId, CancellationToken cancellationToken)
    {
        var returnEntity = await _context.InvoiceReturns
            .Include(item => item.Invoice)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == returnId, cancellationToken)
            ?? throw new InvalidOperationException("برگشت یافت نشد.");

        if (WorkflowStatus.Normalize(returnEntity.Status) == WorkflowStatus.Approved)
        {
            throw new InvalidOperationException("برگشت قبلاً نهایی شده است.");
        }

        returnEntity.Status = WorkflowStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);

        var voucher = await _ledgerService.PostReturnAsync(returnEntity, cancellationToken);

        returnEntity.VoucherHeaderId = voucher.Id;
        returnEntity.VoucherDocumentNumber = voucher.VoucherNumber?.ToString();
        returnEntity.Status = WorkflowStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    // ── Quotations ────────────────────────────────────────────────────────────

    public async Task<QuotationIndexVM> BuildQuotationIndexAsync(string quotationType, string? searchTerm, CancellationToken cancellationToken)
    {
        var normalized = quotationType == "Purchase" ? "Purchase" : "Sale";
        var query = _context.CommercialQuotations.AsNoTracking()
            .Where(item => item.QuotationType == normalized)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = PersianTextNormalizer.Normalize(searchTerm);
            query = query.Where(item => item.QuotationNumber.Contains(term) || item.PartyName.Contains(term));
        }

        var items = await query.OrderByDescending(item => item.QuotationDate).ThenByDescending(item => item.Id)
            .Select(item => new QuotationRowVM
            {
                Id = item.Id,
                QuotationNumber = item.QuotationNumber,
                QuotationType = item.QuotationType,
                PartyName = item.PartyName,
                DateShamsi = item.DateShamsi,
                ValidUntilShamsi = item.ValidUntilShamsi,
                GrandTotal = item.GrandTotal,
                Status = item.Status,
                ConvertedInvoiceId = item.ConvertedInvoiceId
            })
            .ToListAsync(cancellationToken);

        return new QuotationIndexVM { QuotationType = normalized, SearchTerm = searchTerm, Items = items };
    }

    public async Task<CommercialQuotation> SaveQuotationAsync(
        CommercialQuotation entity,
        IEnumerable<CommercialQuotationLine> lines,
        string? userId,
        CancellationToken cancellationToken)
    {
        var validLines = lines.Where(item => !string.IsNullOrWhiteSpace(item.ItemName) && item.Quantity > 0).ToList();
        if (validLines.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک ردیف معتبر برای پیش‌فاکتور وارد کنید.");
        }

        if (entity.Id > 0)
        {
            var existing = await _context.CommercialQuotations.Include(item => item.Lines)
                .FirstOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
                ?? throw new InvalidOperationException("پیش‌فاکتور یافت نشد.");

            if (existing.Status == CommercialQuotationStatus.Converted)
            {
                throw new InvalidOperationException("پیش‌فاکتور تبدیل‌شده قابل ویرایش نیست.");
            }

            existing.QuotationNumber = entity.QuotationNumber;
            existing.CommercialPartyId = entity.CommercialPartyId;
            existing.PartyName = entity.PartyName;
            existing.NationalCodeOrEconomicId = entity.NationalCodeOrEconomicId;
            existing.QuotationDate = entity.QuotationDate;
            existing.DateShamsi = entity.DateShamsi;
            existing.ValidUntil = entity.ValidUntil;
            existing.ValidUntilShamsi = entity.ValidUntilShamsi;
            existing.SubTotal = validLines.Sum(item => item.LineSubTotal);
            existing.VatAmount = validLines.Sum(item => item.LineVatAmount);
            existing.GrandTotal = validLines.Sum(item => item.LineGrandTotal);
            existing.DiscountAmount = entity.DiscountAmount;
            existing.Notes = entity.Notes?.Trim();
            existing.Status = entity.Status;

            if (existing.Lines.Count > 0)
            {
                _context.CommercialQuotationLines.RemoveRange(existing.Lines);
                existing.Lines.Clear();
            }

            existing.Lines = validLines.Select(line => new CommercialQuotationLine
            {
                ProductId = line.ProductId,
                ItemName = line.ItemName.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountRate = line.DiscountRate,
                DiscountAmount = line.DiscountAmount,
                LineSubTotal = line.LineSubTotal,
                LineVatAmount = line.LineVatAmount,
                LineGrandTotal = line.LineGrandTotal
            }).ToList();

            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        entity.SubTotal = validLines.Sum(item => item.LineSubTotal);
        entity.VatAmount = validLines.Sum(item => item.LineVatAmount);
        entity.GrandTotal = validLines.Sum(item => item.LineGrandTotal);
        entity.Status = string.IsNullOrWhiteSpace(entity.Status) ? CommercialQuotationStatus.Draft : entity.Status;
        entity.CreatedAt = DateTime.Now;
        entity.CreatedByUserId = userId;
        entity.Lines = validLines.Select(line => new CommercialQuotationLine
        {
            ProductId = line.ProductId,
            ItemName = line.ItemName.Trim(),
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountRate = line.DiscountRate,
            DiscountAmount = line.DiscountAmount,
            LineSubTotal = line.LineSubTotal,
            LineVatAmount = line.LineVatAmount,
            LineGrandTotal = line.LineGrandTotal
        }).ToList();

        _context.CommercialQuotations.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<(Invoice Invoice, VoucherHeader? Voucher)> ConvertQuotationToInvoiceAsync(
        int quotationId,
        string? invoiceType,
        string dateShamsi,
        DateTime invoiceDate,
        string? userId,
        CancellationToken cancellationToken)
    {
        var quotation = await _context.CommercialQuotations
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == quotationId, cancellationToken)
            ?? throw new InvalidOperationException("پیش‌فاکتور یافت نشد.");

        if (quotation.Status == CommercialQuotationStatus.Converted)
        {
            throw new InvalidOperationException("این پیش‌فاکتور قبلاً به فاکتور تبدیل شده است.");
        }

        var type = string.Equals(invoiceType, "Purchase", StringComparison.OrdinalIgnoreCase) ? "Purchase" : "Sale";
        var invoice = new Invoice
        {
            InvoiceNumber = await BuildNextInvoiceNumberAsync(type, cancellationToken),
            InvoiceType = type,
            DateShamsi = string.IsNullOrWhiteSpace(dateShamsi) ? FinancialAppServiceProxy.GetTodayShamsi() : PersianTextNormalizer.Normalize(dateShamsi),
            PartyName = quotation.PartyName,
            NationalCodeOrEconomicId = quotation.NationalCodeOrEconomicId,
            CommercialPartyId = quotation.CommercialPartyId,
            SubTotal = quotation.SubTotal,
            VatAmount = quotation.VatAmount,
            GrandTotal = quotation.GrandTotal,
            Notes = quotation.Notes,
            WorkflowStatus = WorkflowStatus.Draft,
            Amount = quotation.GrandTotal,
            VendorName = quotation.PartyName,
            InvoiceDate = invoiceDate.Date,
            CreatedAt = DateTime.Now,
            CreatedByUserId = userId,
            Items = quotation.Lines.Select(line => new InvoiceItem
            {
                ProductId = line.ProductId,
                ItemName = line.ItemName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineSubTotal = line.LineSubTotal,
                LineVatAmount = line.LineVatAmount,
                LineGrandTotal = line.LineGrandTotal
            }).ToList()
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        quotation.Status = CommercialQuotationStatus.Converted;
        quotation.ConvertedInvoiceId = invoice.Id;
        await _context.SaveChangesAsync(cancellationToken);

        return (invoice, null);
    }

    // ── Settlements ───────────────────────────────────────────────────────────

    public async Task<SettlementTransaction> RegisterSettlementAsync(
        int invoiceId,
        decimal amount,
        string? referenceNumber,
        string? paymentMethod,
        string dateShamsi,
        DateTime settlementDate,
        string? notes,
        string? userId,
        bool postNow,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("مبلغ تسویه باید بزرگ‌تر از صفر باشد.");
        }

        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("فاکتور یافت نشد.");

        var settlement = new SettlementTransaction
        {
            SettlementType = invoice.InvoiceType,
            InvoiceId = invoice.Id,
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? await BuildNextSettlementReferenceAsync(cancellationToken) : referenceNumber.Trim(),
            SettlementDate = settlementDate.Date,
            DateShamsi = string.IsNullOrWhiteSpace(dateShamsi) ? FinancialAppServiceProxy.GetTodayShamsi() : PersianTextNormalizer.Normalize(dateShamsi),
            Amount = amount,
            PaymentMethod = paymentMethod?.Trim(),
            Notes = notes?.Trim(),
            Status = WorkflowStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        };

        _context.SettlementTransactions.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        if (postNow)
        {
            await _ledgerService.PostSettlementAsync(settlement, cancellationToken);
            settlement.Status = WorkflowStatus.Approved;
            settlement.VoucherHeaderId = await _context.VoucherHeaders.AsNoTracking()
                .Where(item => item.DocumentNumber == $"{(invoice.InvoiceType == "Purchase" ? "PAY" : "REC")}-{settlement.Id}")
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return settlement;
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    public async Task<InvoiceAttachment> AddAttachmentAsync(
        int invoiceId,
        string fileName,
        string storedPath,
        string contentType,
        long sizeBytes,
        InvoiceAttachmentCategory category,
        string? description,
        string? userId,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.AsNoTracking().AnyAsync(item => item.Id == invoiceId, cancellationToken);
        if (!invoice)
        {
            throw new InvalidOperationException("فاکتور یافت نشد.");
        }

        var attachment = new InvoiceAttachment
        {
            InvoiceId = invoiceId,
            FileName = fileName,
            StoredPath = storedPath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Category = category,
            Description = description?.Trim(),
            UploadedByUserId = userId,
            UploadedAt = DateTime.Now
        };

        _context.InvoiceAttachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public async Task<InvoiceAttachment?> GetAttachmentAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.InvoiceAttachments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<bool> DeleteAttachmentAsync(int id, CancellationToken cancellationToken)
    {
        var attachment = await _context.InvoiceAttachments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (attachment == null)
        {
            return false;
        }

        _context.InvoiceAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Invoice> LoadInvoiceWithVoucherAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Invoices.Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("فاکتور یافت نشد.");
    }

    public async Task<string> BuildNextInvoiceNumberAsync(string type, CancellationToken cancellationToken)
    {
        var prefix = type == "Purchase" ? "PUR" : "SAL";
        var count = await _context.Invoices.CountAsync(item => item.InvoiceType == type, cancellationToken);
        return $"{prefix}-{count + 1:00000}";
    }

    public async Task<string> BuildNextReturnNumberAsync(string type, CancellationToken cancellationToken)
    {
        var prefix = type == "Purchase" ? "PRET" : "SRET";
        var count = await _context.InvoiceReturns.CountAsync(item => item.ReturnType == type, cancellationToken);
        return $"{prefix}-{count + 1:00000}";
    }

    public async Task<string> BuildNextQuotationNumberAsync(string type, CancellationToken cancellationToken)
    {
        var prefix = type == "Purchase" ? "PQUO" : "SQUO";
        var count = await _context.CommercialQuotations.CountAsync(item => item.QuotationType == type, cancellationToken);
        return $"{prefix}-{count + 1:00000}";
    }

    private async Task<string> BuildNextSettlementReferenceAsync(CancellationToken cancellationToken)
    {
        var count = await _context.SettlementTransactions.CountAsync(cancellationToken);
        return $"STL-{count + 1:00000}";
    }

    public static string ToShamsi(DateTime? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        var persian = new PersianCalendar();
        var date = value.Value;
        return $"{persian.GetYear(date):0000}/{persian.GetMonth(date):00}/{persian.GetDayOfMonth(date):00}";
    }
}

/// <summary>
/// Small proxy to reuse the existing Shamsi helpers in the Financial app service
/// without needing the full service. Keeps the commercial module decoupled.
/// </summary>
internal static class FinancialAppServiceProxy
{
    public static string GetTodayShamsi()
    {
        var persianCalendar = new PersianCalendar();
        var now = DateTime.Now;
        return $"{persianCalendar.GetYear(now):0000}/{persianCalendar.GetMonth(now):00}/{persianCalendar.GetDayOfMonth(now):00}";
    }
}