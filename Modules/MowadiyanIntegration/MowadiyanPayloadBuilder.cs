using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.InvoiceStructuring;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Maps internal <see cref="InvoiceRecord"/> / <see cref="InvoiceLineItem"/> data
/// to the official Mowadiyan (سامانه مودیان) JSON payload structure
/// (Header, Body, Extension, LineItems).
/// </summary>
public interface IMowadiyanPayloadBuilder
{
    /// <summary>Builds the Mowadiyan invoice payload from a local invoice record.</summary>
    MowadiyanInvoicePayload Build(InvoiceRecord invoice, MowadiyanSettings settings);

    /// <summary>Serializes the payload to its canonical JSON string.</summary>
    string Serialize(MowadiyanInvoicePayload payload);
}

/// <inheritdoc />
public sealed class MowadiyanPayloadBuilder : IMowadiyanPayloadBuilder
{
    private readonly ILogger<MowadiyanPayloadBuilder> _logger;

    public MowadiyanPayloadBuilder(ILogger<MowadiyanPayloadBuilder> logger)
    {
        _logger = logger;
    }

    public MowadiyanInvoicePayload Build(InvoiceRecord invoice, MowadiyanSettings settings)
    {
        var lineItems = MapLineItems(invoice.LineItems);

        var payload = new MowadiyanInvoicePayload
        {
            Header = new MowadiyanHeader
            {
                InvoiceNumber = invoice.InvoiceNumber,
                Date = string.IsNullOrWhiteSpace(invoice.DateShamsi)
                    ? JalaliDateHelper.TodayShamsi()
                    : invoice.DateShamsi,
                InvoiceType = MapInvoiceType(invoice.InvoiceType),
                TotalAmount = invoice.SubTotal,
                TotalDiscount = invoice.DiscountAmount,
                VatAmount = invoice.VatAmount,
                DutyAmount = invoice.DutyAmount,
                FinalAmount = invoice.FinalAmount,
                VatRate = invoice.VatRate,
                SellerEconomicCode = invoice.Seller?.EconomicCode ?? settings.EconomicCode,
                SellerName = invoice.Seller?.Name,
                BuyerEconomicCode = invoice.Buyer?.EconomicCode,
                BuyerNationalId = invoice.Buyer?.NationalId,
                BuyerName = invoice.Buyer?.Name
            },
            Body = new MowadiyanBody { LineItems = lineItems },
            Extension = new MowadiyanExtension
            {
                Source = "Rhyno ERP",
                InternalId = invoice.Id.ToString("N"),
                Notes = invoice.Notes
            }
        };

        _logger.LogInformation(
            "Mowadiyan payload built for invoice {InvoiceNumber} with {LineCount} line items",
            invoice.InvoiceNumber, lineItems.Count);

        return payload;
    }

    public string Serialize(MowadiyanInvoicePayload payload) => payload.ToJson();

    private static List<MowadiyanLineItem> MapLineItems(IEnumerable<InvoiceLineItem> items)
    {
        var result = new List<MowadiyanLineItem>();
        var order = 1;

        foreach (var item in items.OrderBy(i => i.DisplayOrder))
        {
            result.Add(new MowadiyanLineItem
            {
                LineNumber = order++,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineAmount = item.LineAmount,
                Kind = item.Kind,
                IsTaxExempt = item.IsTaxExempt
            });
        }

        return result;
    }

    private static string MapInvoiceType(string invoiceType)
    {
        // Mowadiyan invoice type codes: 01 = sale (فروش), 02 = purchase (خرید).
        return string.Equals(invoiceType, "Purchase", StringComparison.OrdinalIgnoreCase) ? "02" : "01";
    }
}