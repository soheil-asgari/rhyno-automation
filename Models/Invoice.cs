using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "شماره فاکتور الزامی است.")]
        [StringLength(50)]
        public string InvoiceNumber { get; set; }

        [Required(ErrorMessage = "مبلغ فاکتور الزامی است.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "نام فروشنده الزامی است.")]
        [StringLength(150)]
        public string VendorName { get; set; }

        [Required(ErrorMessage = "تاریخ فاکتور الزامی است.")]
        public DateTime InvoiceDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}