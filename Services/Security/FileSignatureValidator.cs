namespace OfficeAutomation.Services.Security;

/// <summary>
/// Provides strict magic-byte (binary signature) and MIME validation for file uploads.
/// </summary>
public static class FileSignatureValidator
{
    public static readonly string[] AllowedExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".txt", ".zip"];

    public static readonly string[] AllowedMimeTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff",
        "text/plain",
        "application/zip",
        "application/x-zip-compressed",
        "application/octet-stream"
    ];

    public const long DefaultMaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private static readonly string[] DangerousExtensions =
        [".exe", ".bat", ".cmd", ".com", ".sh", ".ps1", ".msi", ".dll", ".scr", ".vbs", ".js", ".jsp", ".php", ".asp", ".aspx", ".html", ".htm", ".svg"];

    public static bool IsDangerousExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return true;
        return DangerousExtensions.Contains(extension.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<byte[]> ReadHeaderBytesAsync(Stream stream, int count, CancellationToken ct = default)
    {
        var buffer = new byte[count];
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, count - total), ct);
            if (read == 0) break;
            total += read;
        }
        return buffer[..total];
    }

    public static bool IsSignatureValid(string? extension, byte[] signature)
    {
        if (string.IsNullOrWhiteSpace(extension) || signature == null || signature.Length == 0)
            return false;

        static bool Match(byte[] data, ReadOnlySpan<byte> magic)
        {
            if (data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }

        static bool IsWebP(byte[] data)
        {
            // RIFF....WEBP (RIFF at 0..3, WEBP at 8..11)
            if (data.Length < 12) return false;
            return data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                   data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50;
        }

        static bool IsZipOrOpenXml(byte[] data)
        {
            // PK\x03\x04 or empty zip PK\x05\x06 or spanned PK\x07\x08
            if (data.Length < 4) return false;
            return (data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04) ||
                   (data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x05 && data[3] == 0x06) ||
                   (data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x07 && data[3] == 0x08);
        }

        static bool IsCompoundFileBinary(byte[] data)
        {
            // OLECF: D0 CF 11 E0 A1 B1 1A E1
            return Match(data, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
        }

        static bool IsValidText(byte[] data)
        {
            if (data.Length == 0) return true;
            // Reject binary files disguised as .txt: check for null bytes
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b == 0) return false;
            }
            return true;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => Match(signature, new byte[] { 0x25, 0x50, 0x44, 0x46 }), // %PDF
            ".png" => Match(signature, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => Match(signature, new byte[] { 0xFF, 0xD8, 0xFF }),
            ".gif" => Match(signature, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }) || Match(signature, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }), // GIF87a or GIF89a
            ".bmp" => Match(signature, new byte[] { 0x42, 0x4D }), // BM
            ".tiff" or ".tif" => Match(signature, new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || Match(signature, new byte[] { 0x4D, 0x4D, 0x00, 0x2A }), // II*. or MM.*
            ".webp" => IsWebP(signature),
            ".docx" or ".xlsx" => IsZipOrOpenXml(signature), // Office OpenXML is a ZIP archive
            ".doc" or ".xls" => IsCompoundFileBinary(signature), // Legacy Office Compound File (OLE CF)
            ".zip" => IsZipOrOpenXml(signature),
            ".txt" => IsValidText(signature),
            _ => false
        };
    }

    /// <summary>
    /// Validates file name, extension, MIME type, size, and magic-byte header.
    /// Returns null if valid, or a localized Persian error message if invalid.
    /// </summary>
    public static async Task<string?> ValidateUploadAsync(
        string? fileName,
        string? contentType,
        long fileSize,
        Stream fileStream,
        long maxSizeBytes = DefaultMaxFileSizeBytes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "نام فایل نامعتبر است.";

        if (fileSize <= 0)
            return "فایل انتخابی خالی است.";

        if (fileSize > maxSizeBytes)
            return $"حجم فایل بیش از حد مجاز است (حداکثر {maxSizeBytes / (1024 * 1024)} مگابایت).";

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || IsDangerousExtension(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
            return "فرمت فایل مجاز نیست. پسوندهای مجاز: PDF، DOC، DOCX، XLS، XLSX، JPG، PNG، GIF، WEBP، BMP، TIFF، TXT، ZIP.";

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedMimeTypes.Contains(contentType.ToLowerInvariant()))
            return "نوع محتوای فایل (MIME-Type) مجاز نیست.";

        if (!fileStream.CanRead)
            return "امکان خواندن فایل وجود ندارد.";

        // Read at least 16 bytes to check headers (e.g. WEBP needs 12 bytes, OLECF needs 8 bytes)
        var originalPos = fileStream.CanSeek ? fileStream.Position : 0;
        var header = await ReadHeaderBytesAsync(fileStream, 16, ct);
        if (fileStream.CanSeek)
        {
            fileStream.Position = originalPos;
        }

        if (!IsSignatureValid(extension, header))
        {
            return "امضای باینری فایل (Magic-Byte) با نوع فایل مطابقت ندارد. فایل نامعتبر یا دستکاری شده است.";
        }

        return null;
    }
}
