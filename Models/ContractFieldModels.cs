using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// Defines a type-specific field template for a contract type (EAV approach).
/// Entries are seeded for each ContractType (Lease, NDA, SLA, Service, etc.)
/// and determine which dynamic fields appear on the Create/Edit forms.
/// </summary>
public class ContractTypeField
{
    public int Id { get; set; }

    /// <summary>The contract type this field belongs to (e.g. "اجاره", "NDA", etc.)</summary>
    [Required]
    [StringLength(60)]
    public string ContractType { get; set; } = string.Empty;

    /// <summary>Unique key for the field (e.g. "Lease_RentAmount", "NDA_DisclosurePurpose")</summary>
    [Required]
    [StringLength(120)]
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>Display label shown to the user (e.g. "مبلغ اجاره ماهانه")</summary>
    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Field type: Text, Number, Decimal, Date, Boolean, Select, TextArea</summary>
    [Required]
    [StringLength(30)]
    public string FieldType { get; set; } = "Text";

    /// <summary>Optional comma-separated options for Select field type</summary>
    [StringLength(2000)]
    public string? Options { get; set; }

    /// <summary>Whether this field is required for the contract type</summary>
    public bool IsRequired { get; set; }

    /// <summary>Display sort order within the contract type group</summary>
    public int SortOrder { get; set; }

    /// <summary>Section/group heading this field belongs to (nullable)</summary>
    [StringLength(200)]
    public string? SectionName { get; set; }

    /// <summary>Placeholder text for input</summary>
    [StringLength(200)]
    public string? Placeholder { get; set; }

    /// <summary>Validation regex pattern (optional)</summary>
    [StringLength(500)]
    public string? ValidationPattern { get; set; }

    /// <summary>Validation error message when pattern does not match</summary>
    [StringLength(500)]
    public string? ValidationMessage { get; set; }
}

/// <summary>
/// Stores the actual value for a dynamic field on a specific Contract.
/// </summary>
public class ContractFieldValue
{
    public int Id { get; set; }

    public int ContractId { get; set; }

    [ForeignKey(nameof(ContractId))]
    public Contract Contract { get; set; } = null!;

    /// <summary>FK to the field definition</summary>
    public int ContractTypeFieldId { get; set; }

    [ForeignKey(nameof(ContractTypeFieldId))]
    public ContractTypeField FieldDefinition { get; set; } = null!;

    /// <summary>The stored value (stored as string; parsed per FieldDefinition.FieldType)</summary>
    public string? Value { get; set; }
}
