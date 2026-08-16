using System.Runtime.CompilerServices;

// Grants the xUnit test project (OfficeAutomation.Tests) access to the internal-only
// test seams exposed by this assembly (e.g. PersianTextNormalizer and the CAPTCHA OCR
// wrapper backing TaxExporterService) without a live portal session.
[assembly: InternalsVisibleTo("OfficeAutomation.Tests")]
