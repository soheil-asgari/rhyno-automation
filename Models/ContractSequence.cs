using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// Dedicated sequence store for contract numbers per calendar year.
/// Provides atomic running sequence increments independent of existing contract numbers format.
/// </summary>
[Table("ContractSequences")]
public class ContractSequence
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Year { get; set; }

    [ConcurrencyCheck]
    public int LastSequenceNumber { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
