namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Runtime configuration for the Mowadiyan (سامانه مودیان) Tax Portal integration,
/// bound from the "Mowadiyan" section of <c>appsettings.json</c>.
/// </summary>
public sealed class MowadiyanOptions
{
    public const string SectionName = "Mowadiyan";

    /// <summary>When true, the sandbox client simulates the Tax Portal API.</summary>
    public bool IsSandbox { get; set; } = true;

    /// <summary>Base URL of the Tax Portal API (production: https://tp.tax.gov.ir).</summary>
    public string BaseUrl { get; set; } = "https://tp.tax.gov.ir";

    /// <summary>Client ID (شناسه سرویس گیرنده).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Memory key / client secret (کلید حافظه).</summary>
    public string? MemorySecret { get; set; }

    /// <summary>Path to the PKCS#12 (.pfx) private key certificate.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Password of the private key certificate.</summary>
    public string? PrivateKeyPassword { get; set; }

    /// <summary>Economic code (کد اقتصادی) of the taxpayer.</summary>
    public string EconomicCode { get; set; } = string.Empty;

    /// <summary>Batch size for the background submission queue.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Maximum retry attempts before a record is marked RetryLimitExceeded.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Initial delay (seconds) for exponential backoff.</summary>
    public int InitialBackoffSeconds { get; set; } = 10;

    /// <summary>Maximum backoff delay (seconds).</summary>
    public int MaxBackoffSeconds { get; set; } = 600;

    /// <summary>
    /// Executable used to run the Python Playwright exporter
    /// (<c>scripts/tax_exporter.py</c>). Defaults to <c>python</c>.
    /// </summary>
    public string PythonExecutable { get; set; } = "python";

    /// <summary>
    /// Absolute path to <c>tax_exporter.py</c>. When empty the script is resolved
    /// relative to the content root (<c>{ContentRoot}/scripts/tax_exporter.py</c>).
    /// </summary>
    public string? PythonScriptPath { get; set; }

    /// <summary>
    /// Directory where the Python exporter writes downloaded .xlsx/.zip files.
    /// Defaults to <c>{ContentRoot}/wwwroot/uploads/mowadiyan</c>.
    /// </summary>
    public string? ExportOutDir { get; set; }
}