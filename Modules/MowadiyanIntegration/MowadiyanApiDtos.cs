using System.Text.Json.Serialization;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>Token inquiry response from the Tax Portal.</summary>
public sealed class MowadiyanTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

/// <summary>Result of submitting one or more invoices to the Tax Portal.</summary>
public sealed class MowadiyanSubmitResult
{
    public bool Success { get; set; }

    /// <summary>Tax Reference ID (شماره پیگیری مالیاتی) returned by the portal.</summary>
    public string? TaxReferenceId { get; set; }

    /// <summary>Fiscal ID (شناسه مالیاتی) of the submitted invoice.</summary>
    public string? FiscalId { get; set; }

    /// <summary>Raw JSON response body.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>Error message when the submission is rejected.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Status inquiry request by fiscal ID / tax reference ID.</summary>
public sealed class MowadiyanStatusRequest
{
    public string? TaxReferenceId { get; set; }
    public string? FiscalId { get; set; }
}

/// <summary>Status inquiry response.</summary>
public sealed class MowadiyanStatusResult
{
    public bool Success { get; set; }

    /// <summary>Portal status, e.g. SUCCESS / REJECTED / PENDING.</summary>
    public string? Status { get; set; }

    /// <summary>Tax Reference ID (شماره پیگیری مالیاتی).</summary>
    public string? TaxReferenceId { get; set; }

    /// <summary>Fiscal ID (شناسه مالیاتی).</summary>
    public string? FiscalId { get; set; }

    /// <summary>Raw JSON response body.</summary>
    public string? ResponseJson { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>Base envelope payload sent to the Tax Portal.</summary>
public sealed class MowadiyanSubmitRequest
{
    [JsonPropertyName("invoice")]
    public string InvoiceJson { get; set; } = string.Empty;

    [JsonPropertyName("envelope")]
    public string? Pkcs7Envelope { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("signature_key_id")]
    public string? SignatureKeyId { get; set; }

    [JsonPropertyName("signature_algorithm")]
    public string? SignatureAlgorithm { get; set; }

    [JsonPropertyName("signature_headers")]
    public string? SignatureHeaders { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("trace_number")]
    public string? TraceNumber { get; set; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }
}