namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// Category of a financial attachment attached to a commercial invoice.
/// </summary>
public enum InvoiceAttachmentCategory
{
    InvoiceScan = 1,
    Contract = 2,
    Receipt = 3,
    SupportingDocument = 4
}