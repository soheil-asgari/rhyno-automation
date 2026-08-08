using System.Text.Json;
using Json.Schema;

namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// Validates extracted invoice JSON against the canonical <see cref="InvoiceSchema"/>.
/// </summary>
public sealed class InvoiceJsonSchemaValidator
{
    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(InvoiceSchema.Json));

    /// <summary>
    /// Validates an extracted invoice document. Returns an empty list when valid.
    /// </summary>
    public IReadOnlyList<InvoiceSchemaIssue> Validate(JsonElement? document)
    {
        if (document is null || document.Value.ValueKind == JsonValueKind.Undefined)
        {
            return new[] { new InvoiceSchemaIssue("$", "ساختار فاکتور قابل اعتبارسنجی نیست.") };
        }

        var instance = document.Value;
        var results = Schema.Value.Evaluate(
            instance,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return Array.Empty<InvoiceSchemaIssue>();
        }

        var issues = new List<InvoiceSchemaIssue>();
        foreach (var detail in results.Details)
        {
            if (detail.IsValid)
            {
                continue;
            }

            var instancePath = detail.InstanceLocation.ToString();
            var evaluationPath = detail.EvaluationPath.ToString();
            var keyword = evaluationPath.Split('/').LastOrDefault()?.Trim('/');

            issues.Add(new InvoiceSchemaIssue(
                instancePath,
                FormatMessage(keyword ?? "validation", instancePath)));
        }

        return issues;
    }

    /// <summary>
    /// Builds a JSON document from the extracted fields using the schema's snake_case names.
    /// </summary>
    public static JsonElement? ToDocument(Ocr.ExtractedInvoice? extracted)
    {
        if (extracted is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(new
        {
            invoice_number = extracted.InvoiceNumber,
            date = extracted.Date?.ToString("yyyy-MM-dd")
                   ?? (string.IsNullOrWhiteSpace(extracted.DateShamsi) ? string.Empty : extracted.DateShamsi),
            total_amount = extracted.TotalAmount,
            seller_name = extracted.SellerName,
            buyer_name = extracted.BuyerName
        });
    }

    private static string FormatMessage(string keyword, string instancePath)
    {
        var field = instancePath.TrimStart('/');
        return keyword switch
        {
            "required" => "فیلدهای الزامی (شماره فاکتور، تاریخ، مبلغ کل، نام فروشنده، نام خریدار) باید مقدار داشته باشند.",
            "minLength" => $"فیلد «{field}» نباید خالی باشد.",
            "type" => $"نوع داده فیلد «{field}» نامعتبر است.",
            "format" => $"تاریخ فیلد «{field}» نامعتبر است.",
            "minimum" => $"مبلغ فیلد «{field}» نمیتواند منفی باشد.",
            _ => $"مقدار فیلد «{field}» با ساختار استاندارد فاکتور سازگار نیست."
        };
    }
}
