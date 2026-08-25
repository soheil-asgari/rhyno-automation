using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// Validates dynamic field values per their field definition (type, required, regex).
/// </summary>
public interface IContractFieldValidator
{
    /// <summary>
    /// Validates the submitted field values against the field definitions for the given contract type.
    /// Returns a dictionary of (FieldKey → error message). Empty dictionary = valid.
    /// </summary>
    Dictionary<string, string> ValidateFields(
        string contractType,
        List<ContractTypeFieldVM> fieldDefinitions,
        Dictionary<string, string> submittedValues);
}

public sealed class ContractFieldValidator : IContractFieldValidator
{
    public Dictionary<string, string> ValidateFields(
        string contractType,
        List<ContractTypeFieldVM> fieldDefinitions,
        Dictionary<string, string> submittedValues)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(contractType))
            return errors;

        var relevantFields = fieldDefinitions
            .Where(f => f.ContractType == contractType)
            .OrderBy(f => f.SortOrder)
            .ToList();

        foreach (var field in relevantFields)
        {
            var hasValue = submittedValues.TryGetValue(field.FieldKey, out var rawValue);
            var value = rawValue?.Trim() ?? string.Empty;

            // Required check
            if (field.IsRequired && (!hasValue || string.IsNullOrWhiteSpace(value)))
            {
                errors[field.FieldKey] = $"فیلد \"{field.DisplayName}\" اجباری است.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;

            // Type-specific validation
            switch (field.FieldType)
            {
                case "Number":
                    if (!int.TryParse(value, out _))
                        errors[field.FieldKey] = $"فیلد \"{field.DisplayName}\" باید یک عدد صحیح باشد.";
                    break;

                case "Decimal":
                    if (!decimal.TryParse(value, out _))
                        errors[field.FieldKey] = $"فیلد \"{field.DisplayName}\" باید یک عدد اعشاری باشد.";
                    break;

                case "Date":
                    if (!DateTime.TryParse(value, out _))
                        errors[field.FieldKey] = $"فیلد \"{field.DisplayName}\" باید یک تاریخ معتبر باشد.";
                    break;

                case "Boolean":
                    var lower = value.ToLowerInvariant();
                    if (lower is not ("true" or "false" or "1" or "0" or "yes" or "no" or "بله" or "خیر"))
                        errors[field.FieldKey] = $"فیلد \"{field.DisplayName}\" باید یک مقدار بولی (بله/خیر) باشد.";
                    break;

                case "Select":
                    if (!string.IsNullOrWhiteSpace(field.Options))
                    {
                        var validOptions = field.Options
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!validOptions.Contains(value, StringComparer.OrdinalIgnoreCase))
                            errors[field.FieldKey] = $"مقدار فیلد \"{field.DisplayName}\" باید یکی از گزینه‌های مجاز باشد.";
                    }
                    break;
            }

            // Regex pattern validation
            if (!string.IsNullOrWhiteSpace(field.ValidationPattern) && !string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, field.ValidationPattern))
                    {
                        errors[field.FieldKey] = field.ValidationMessage
                            ?? $"فرمت فیلد \"{field.DisplayName}\" معتبر نیست.";
                    }
                }
                catch
                {
                    // If the regex itself is invalid, skip pattern validation silently
                }
            }
        }

        return errors;
    }
}
