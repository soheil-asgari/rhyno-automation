using System.Collections.Frozen;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Single source of truth for licensable business modules in the application.
/// Used by [RequiresModule], LicenseInfo, LicenseIssuer CLI, and UI navigation gating.
/// </summary>
public static class LicensableModules
{
    public const string Contracts = "Contracts";
    public const string Financial = "Financial";
    public const string Warehouse = "Warehouse";
    public const string HumanCapital = "HumanCapital";
    public const string OfficeAutomation = "OfficeAutomation";
    public const string Workflow = "Workflow";
    public const string AiAssistant = "AiAssistant";

    /// <summary>
    /// All defined licensable module codes.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Contracts,
        Financial,
        Warehouse,
        HumanCapital,
        OfficeAutomation,
        Workflow,
        AiAssistant
    ];

    private static readonly FrozenSet<string> ValidModuleSet = All.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given module code is valid (case-insensitive, alias-aware).
    /// </summary>
    public static bool IsValid(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return false;
        var normalized = Normalize(moduleName);
        return ValidModuleSet.Contains(normalized);
    }

    /// <summary>
    /// Normalizes common aliases (e.g. Finance -> Financial, Contract -> Contracts, HR -> HumanCapital).
    /// </summary>
    public static string Normalize(string moduleName)
    {
        var trimmed = moduleName.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "finance" => Financial,
            "financial" => Financial,
            "contract" => Contracts,
            "contracts" => Contracts,
            "hr" or "human" or "humancapital" => HumanCapital,
            "inventory" or "warehouse" => Warehouse,
            "office" or "officeautomation" or "secretariat" => OfficeAutomation,
            "workflow" => Workflow,
            "ai" or "aiassistant" => AiAssistant,
            _ => trimmed
        };
    }

    /// <summary>
    /// Returns a friendly Persian display name for the specified module.
    /// </summary>
    public static string GetDisplayName(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return "ماژول درخواستی";

        return Normalize(moduleName) switch
        {
            Contracts => "قراردادها (Contracts)",
            Financial => "مالی و حسابداری (Financial)",
            Warehouse => "انبار و عملیات (Warehouse)",
            HumanCapital => "سرمایه انسانی (Human Capital)",
            OfficeAutomation => "اتوماسیون اداری و دبیرخانه (Office Automation)",
            Workflow => "مدیریت فرایند و کارتابل (Workflow)",
            AiAssistant => "دستیار هوشمند (AI Assistant)",
            _ => moduleName
        };
    }

    /// <summary>
    /// Scans the given assembly (or calling/entry assembly) for all concrete MVC and API controllers,
    /// ensuring every single one is explicitly annotated with either [RequiresModule] or [LicenseExempt].
    /// Returns the list of unannotated controller types (empty if 100% compliant).
    /// </summary>
    public static IReadOnlyList<Type> FindUncoveredControllers(System.Reflection.Assembly? assembly = null)
    {
        assembly ??= typeof(LicensableModules).Assembly;

        var controllerBaseType = typeof(Microsoft.AspNetCore.Mvc.ControllerBase);

        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && controllerBaseType.IsAssignableFrom(t))
            .Where(t =>
            {
                var hasRequiresModule = t.GetCustomAttributes(inherit: true).Any(a =>
                    a.GetType().Name == nameof(RequiresModuleAttribute) ||
                    a.GetType().FullName == "OfficeAutomation.Filters.RequiresModuleAttribute" ||
                    a.GetType().FullName == "OfficeAutomation.Services.Licensing.RequiresModuleAttribute");

                var hasLicenseExempt = t.GetCustomAttributes(inherit: true).Any(a =>
                    a.GetType().Name == nameof(LicenseExemptAttribute) ||
                    a.GetType().FullName == "OfficeAutomation.Filters.LicenseExemptAttribute" ||
                    a.GetType().FullName == "OfficeAutomation.Services.Licensing.LicenseExemptAttribute");

                return !hasRequiresModule && !hasLicenseExempt;
            })
            .ToList();
    }

    /// <summary>
    /// Enforces that all controllers in the application assembly are annotated with either
    /// [RequiresModule] or [LicenseExempt]. Throws InvalidOperationException if any unannotated
    /// controller is found.
    /// </summary>
    public static void EnsureAllControllersAnnotated(System.Reflection.Assembly? assembly = null)
    {
        var uncovered = FindUncoveredControllers(assembly);
        if (uncovered.Count > 0)
        {
            var names = string.Join(", ", uncovered.Select(t => t.FullName ?? t.Name));
            throw new InvalidOperationException(
                $"[Licensing Safety Error] The following controller(s) have NEITHER [RequiresModule] NOR [LicenseExempt]: {names}. " +
                "Every controller must explicitly declare its sellable module via [RequiresModule(LicensableModules.X)] or be marked [LicenseExempt].");
        }
    }
}
