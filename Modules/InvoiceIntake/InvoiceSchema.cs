namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// JSON Schema describing the canonical (standard) invoice structure used by the intake
/// pipeline to validate extracted OCR fields before hand-off.
/// </summary>
public static class InvoiceSchema
{
    // Mirrors the ExtractedInvoice / ExtractedInvoiceFieldsDto field names.
    public const string Json =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://rhyno.app/schemas/invoice-intake/v1",
          "title": "Standard Invoice",
          "type": "object",
          "additionalProperties": true,
          "required": [ "invoice_number", "date", "total_amount", "seller_name", "buyer_name" ],
          "properties": {
            "invoice_number": { "type": "string", "minLength": 1 },
            "date": { "type": "string", "format": "date" },
            "total_amount": { "type": "number", "minimum": 0 },
            "seller_name": { "type": "string" },
            "buyer_name": { "type": "string" }
          }
        }
        """;
}
