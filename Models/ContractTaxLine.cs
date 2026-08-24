using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// ردیف مالیات / ارزش افزوده برای یک قرارداد
/// امکان تعریف چند نوع مالیات (مالیات بر ارزش افزوده، مالیات بر عملکرد و ...)
/// </summary>
public class ContractTaxLine
{
    public int Id { get; set; }

    [Required]
    public int ContractId { get; set; }

    public Contract? Contract { get; set; }

    /// <summary>عنوان مالیات (مثلاً "مالیات بر ارزش افزوده", "مالیات بر عملکرد")</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "عنوان مالیات")]
    public string TaxName { get; set; } = "VAT";

    /// <summary>نرخ مالیات به درصد (مثلاً 9 برای 9%)</summary>
    [Required]
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "نرخ (%)")]
    public decimal TaxRatePercent { get; set; }

    /// <summary>مبلغ پایه محاسبه مالیات</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ پایه")]
    public decimal TaxBaseAmount { get; set; }

    /// <summary>مبلغ مالیات محاسبه‌شده</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ مالیات")]
    public decimal TaxAmount { get; set; }

    /// <summary>روش محاسبه: Exclusive, Inclusive, None</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "روش محاسبه")]
    public string CalculationMethod { get; set; } = OfficeAutomation.Models.TaxCalculationMethod.Exclusive;

    /// <summary>بخش/دسته‌بندی مالیاتی (مثلاً "K1", "K3")</summary>
    [StringLength(20)]
    [Display(Name = "دسته مالیاتی")]
    public string? TaxCategory { get; set; }

    /// <summary>معاف از مالیات</summary>
    [Display(Name = "معاف")]
    public bool IsExempt { get; set; }

    /// <summary>توضیحات</summary>
    [StringLength(500)]
    [Display(Name = "توضیحات")]
    public string? Description { get; set; }

    /// <summary>ارجاع به اصلاحیه (در صورت اعمال از طریق اصلاحیه)</summary>
    public int? AmendmentId { get; set; }

    [ForeignKey(nameof(AmendmentId))]
    public ContractAmendment? Amendment { get; set; }

    [Display(Name = "تاریخ ایجاد")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}