using System.Security.Cryptography;
using System.Text.Json;

namespace LicenseIssuer;

internal class Program
{
    private const string DefaultPrivateKeyPath = "license-private.pem";
    private const string DefaultPublicKeyPath = "license-public.pem";

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            switch (command)
            {
                case "keygen":
                    return HandleKeyGen(args.Skip(1).ToArray());

                case "issue":
                    return await HandleIssue(args.Skip(1).ToArray());

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Unknown command: {command}");
                    Console.ResetColor();
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Details: {ex.InnerException.Message}");
            }
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
===================================================================
Rhyno Automation — License Issuer CLI (Internal Tool)
===================================================================

Commands:
  keygen [--out-dir <dir>]
      Generate a new RSA 2048-bit key pair (license-private.pem & license-public.pem).
      CAUTION: The private key should never leave the issuer environment!

  issue [--config <config.json>] [--out <output.lic>] [--key <private-key.pem>]
      Issue a signed license using a JSON configuration file.

  issue --customer-name <name> --customer-id <id> --modules <m1,m2> --max-users <n>
        --fingerprint <hash> [--type <sub|trial|perpetual>] [--days <30|365>]
        [--perpetual] [--online-check <true|false>] [--support-expires <yyyy-MM-dd>]
        [--key <private.pem>] [--out <output.lic>]
      Issue a signed license via direct CLI parameters.

Examples:
  dotnet run -- keygen
  dotnet run -- issue --customer-name ""Acme Corp"" --customer-id ""CUST-001"" --modules Contracts,Financial --max-users 25 --fingerprint ""TEST-FINGERPRINT-123"" --days 365
");
    }

    private static int HandleKeyGen(string[] args)
    {
        string outDir = Directory.GetCurrentDirectory();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--out-dir" && i + 1 < args.Length)
            {
                outDir = args[++i];
            }
        }

        Directory.CreateDirectory(outDir);
        var privPath = Path.Combine(outDir, "license-private.pem");
        var pubPath = Path.Combine(outDir, "license-public.pem");

        if (File.Exists(privPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Warning: {privPath} already exists. Overwrite? (y/N): ");
            Console.ResetColor();
            var resp = Console.ReadLine();
            if (resp?.Trim().ToLowerInvariant() != "y")
            {
                Console.WriteLine("Keygen aborted.");
                return 0;
            }
        }

        Console.WriteLine("Generating 2048-bit RSA key pair...");
        var (privPem, pubPem) = RsaKeyHelper.GenerateKeyPair(2048);

        File.WriteAllText(privPath, privPem);
        File.WriteAllText(pubPath, pubPem);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[+] Generated private key: {privPath}");
        Console.WriteLine($"[+] Generated public key:  {pubPath}");
        Console.ResetColor();
        Console.WriteLine("\nIMPORTANT: Copy license-public.pem to the main web app (embedded resource),");
        Console.WriteLine("and KEEP license-private.pem secure. NEVER commit or ship the private key!");
        return 0;
    }

    private static async Task<int> HandleIssue(string[] args)
    {
        string? configFile = null;
        string keyFile = DefaultPrivateKeyPath;
        string outputFile = "license.lic";

        var request = new LicenseIssueRequest();
        int days = 365;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--config":
                    if (i + 1 < args.Length) configFile = args[++i];
                    break;
                case "--key":
                    if (i + 1 < args.Length) keyFile = args[++i];
                    break;
                case "--out":
                    if (i + 1 < args.Length) outputFile = args[++i];
                    break;
                case "--customer-name":
                    if (i + 1 < args.Length) request.CustomerName = args[++i];
                    break;
                case "--customer-id":
                    if (i + 1 < args.Length) request.CustomerId = args[++i];
                    break;
                case "--modules":
                    if (i + 1 < args.Length)
                    {
                        var raw = args[++i];
                        request.Modules = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    }
                    break;
                case "--max-users":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var maxU))
                    {
                        request.MaxUsers = maxU;
                    }
                    break;
                case "--fingerprint":
                    if (i + 1 < args.Length) request.HardwareFingerprint = args[++i];
                    break;
                case "--type":
                    if (i + 1 < args.Length) request.LicenseType = args[++i];
                    break;
                case "--days":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var d))
                    {
                        days = d;
                    }
                    break;
                case "--perpetual":
                    request.IsPerpetual = true;
                    request.LicenseType = "perpetual";
                    break;
                case "--online-check":
                    if (i + 1 < args.Length && bool.TryParse(args[++i], out var onlineCheck))
                    {
                        request.OnlineCheckRequired = onlineCheck;
                    }
                    break;
                case "--support-expires":
                    if (i + 1 < args.Length && DateTime.TryParse(args[++i], null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var supportExp))
                    {
                        request.SupportExpiresAtUtc = supportExp;
                    }
                    break;
                case "--module-limits": // format: Contracts:20,Financial:10
                    if (i + 1 < args.Length)
                    {
                        var raw = args[++i];
                        var pairs = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pair in pairs)
                        {
                            var parts = pair.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[1], out var limit))
                            {
                                request.ModuleUserLimits[parts[0].Trim()] = limit;
                            }
                        }
                    }
                    break;
            }
        }

        if (!string.IsNullOrEmpty(configFile))
        {
            if (!File.Exists(configFile))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Config file not found: {configFile}");
                Console.ResetColor();
                return 1;
            }

            var json = await File.ReadAllTextAsync(configFile);
            var loaded = JsonSerializer.Deserialize<LicenseIssueRequest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loaded != null)
            {
                request = loaded;
            }
        }

        if (!request.IsPerpetual && request.ExpiresAtUtc == null)
        {
            request.ExpiresAtUtc = request.IssuedAtUtc.AddDays(days);
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Validation Error: Customer name is required (--customer-name).");
            Console.ResetColor();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(request.HardwareFingerprint))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Validation Error: Hardware fingerprint is required (--fingerprint).");
            Console.ResetColor();
            return 1;
        }

        if (request.Modules.Count == 0)
        {
            request.Modules.Add(OfficeAutomation.Services.Licensing.LicensableModules.Contracts); // default
        }

        // Validate all modules against LicensableModules registry
        var invalidModules = request.Modules
            .Where(m => !OfficeAutomation.Services.Licensing.LicensableModules.IsValid(m))
            .ToList();

        if (invalidModules.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Validation Error: Unknown module(s): {string.Join(", ", invalidModules)}");
            Console.WriteLine($"Valid modules are: {string.Join(", ", OfficeAutomation.Services.Licensing.LicensableModules.All)}");
            Console.ResetColor();
            return 1;
        }

        // Normalize module names to canonical names
        request.Modules = request.Modules
            .Select(m => OfficeAutomation.Services.Licensing.LicensableModules.Normalize(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!File.Exists(keyFile))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Private key file not found: {keyFile}");
            Console.WriteLine("Please generate keys first using: dotnet run -- keygen");
            Console.ResetColor();
            return 1;
        }

        var privatePem = await File.ReadAllTextAsync(keyFile);
        using var rsa = RsaKeyHelper.LoadPrivateKeyFromPem(privatePem);

        var generator = new LicenseTokenGenerator();
        var jwt = generator.GenerateLicenseToken(request, rsa);

        await File.WriteAllTextAsync(outputFile, jwt);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[+] License successfully generated!");
        Console.ResetColor();
        Console.WriteLine($"  License ID:       {request.LicenseId}");
        Console.WriteLine($"  Customer:         {request.CustomerName} ({request.CustomerId})");
        Console.WriteLine($"  Modules:          {string.Join(", ", request.Modules)}");
        Console.WriteLine($"  Max Users:        {request.MaxUsers}");
        if (request.ModuleUserLimits.Count > 0)
        {
            Console.WriteLine($"  Module Limits:    {string.Join(", ", request.ModuleUserLimits.Select(kv => $"{kv.Key}:{kv.Value}"))}");
        }
        Console.WriteLine($"  Online Check:     {(request.OnlineCheckRequired ? "Required" : "False (Offline-Only)")}");
        if (request.SupportExpiresAtUtc.HasValue)
        {
            Console.WriteLine($"  Support Expires:  {request.SupportExpiresAtUtc.Value:yyyy-MM-dd HH:mm:ss UTC}");
        }
        Console.WriteLine($"  Fingerprint:      {request.HardwareFingerprint}");
        Console.WriteLine($"  License Type:     {request.LicenseType}");
        Console.WriteLine($"  Expires:          {(request.IsPerpetual ? "PERPETUAL (Never)" : request.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss UTC"))}");
        Console.WriteLine($"  Saved to:         {Path.GetFullPath(outputFile)}");

        return 0;
    }
}
