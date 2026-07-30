using System.Security.Cryptography;
using System.Text;

namespace OfficeAutomation.Services;

/// <summary>
/// سرویس احراز هویت دو مرحله‌ای مبتنی بر TOTP (RFC 6238)
/// سازگار با Google Authenticator, Microsoft Authenticator و سایر اپلیکیشن‌های استاندارد
/// </summary>
public interface ITotpService
{
    /// <summary>تولید Secret Key جدید برای کاربر</summary>
    string GenerateSecretKey();

    /// <summary>تولید آدرس QR Code برای اسکن در اپلیکیشن Authenticator</summary>
    string GenerateQrCodeUri(string secretKey, string userName, string issuer = "RhynoERP");

    /// <summary>اعتبارسنجی کد TOTP وارد شده توسط کاربر</summary>
    bool ValidateCode(string secretKey, string code, int timeStepSeconds = 30, int allowedDriftSteps = 1);

    /// <summary>تولید کدهای ریکاوری برای مواقع اضطراری</summary>
    string[] GenerateRecoveryCodes(int count = 8);
}

public sealed class TotpService : ITotpService
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public string GenerateSecretKey()
    {
        var bytes = new byte[20];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base32Encode(bytes);
    }

    public string GenerateQrCodeUri(string secretKey, string userName, string issuer = "RhynoERP")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedUser = Uri.EscapeDataString(userName);
        return $"otpauth://totp/{encodedIssuer}:{encodedUser}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secretKey, string code, int timeStepSeconds = 30, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code) || code.Length != 6)
        {
            return false;
        }

        if (!long.TryParse(code, out _))
        {
            return false;
        }

        var secretBytes = Base32Decode(secretKey);
        var currentStep = GetCurrentTimeStep(timeStepSeconds);

        for (var i = -allowedDriftSteps; i <= allowedDriftSteps; i++)
        {
            var computedCode = ComputeTotp(secretBytes, currentStep + i, 6);
            if (string.Equals(computedCode, code, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public string[] GenerateRecoveryCodes(int count = 8)
    {
        var codes = new string[count];
        using var rng = RandomNumberGenerator.Create();
        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            var value = BitConverter.ToUInt64(bytes, 0) % 10_000_000_000_000;
            codes[i] = value.ToString("D12");
        }
        return codes;
    }

    private static long GetCurrentTimeStep(int stepSeconds)
    {
        var secondsSinceEpoch = (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        return secondsSinceEpoch / stepSeconds;
    }

    private static string ComputeTotp(byte[] secretBytes, long timeStep, int digits)
    {
        var timeStepBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeStepBytes);
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeStepBytes);

        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString($"D{digits}");
    }

    #region Base32 Encoding/Decoding

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var result = new StringBuilder();
        var bits = 0;
        var value = 0;

        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                var index = (value >> bits) & 31;
                result.Append(Base32Alphabet[index]);
            }
        }

        if (bits > 0)
        {
            result.Append(Base32Alphabet[(value << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        var normalized = base32.Trim().ToUpperInvariant();
        using var stream = new MemoryStream();
        var bits = 0;
        var value = 0;

        foreach (var c in normalized)
        {
            if (c == '=')
            {
                break;
            }

            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                continue;
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                stream.WriteByte((byte)((value >> bits) & 0xFF));
            }
        }

        return stream.ToArray();
    }

    #endregion
}