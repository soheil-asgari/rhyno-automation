namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// Lifecycle state of an ingested invoice file.
/// </summary>
public enum IntakeFileStatus
{
    // Uploaded but not yet processed by the OCR pipeline.
    Pending = 0,

    // OCR/extraction currently running.
    Processing = 1,

    // Successfully processed (OCR + schema validation attempted).
    Completed = 2,

    // Processing failed (OCR error, invalid content, schema rejection).
    Failed = 3
}
