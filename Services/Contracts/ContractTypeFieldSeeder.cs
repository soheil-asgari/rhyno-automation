using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// Ensures the ContractTypeField definitions exist for each contract type.
/// Runs on every startup, idempotent by checking existing count.
/// </summary>
public sealed class ContractTypeFieldSeeder
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<ContractTypeFieldSeeder> _logger;

    public ContractTypeFieldSeeder(OfficeDbContext context, ILogger<ContractTypeFieldSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingCount = await _context.ContractTypeFields.CountAsync(ct);
        if (existingCount > 0)
        {
            _logger.LogInformation("ContractTypeFieldSeeder: {Count} definitions exist, skip seed.", existingCount);
            return;
        }

        var fields = new List<ContractTypeField>();
        AddLeaseFields(fields);
        AddNdaFields(fields);
        AddServiceFields(fields);
        AddSalesFields(fields);
        AddPurchaseFields(fields);
        AddPartnershipFields(fields);
        AddMaintenanceFields(fields);
        AddConsultingFields(fields);

        _context.ContractTypeFields.AddRange(fields);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("ContractTypeFieldSeeder: seeded {Count} definitions.", fields.Count);
    }

    private static void AddLeaseFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_RentAmount", DisplayName = "مبلغ اجاره ماهانه", FieldType = "Decimal", IsRequired = true, SortOrder = 1, SectionName = "مشخصات اجاره" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_DepositAmount", DisplayName = "مبلغ ودیعه / رهن", FieldType = "Decimal", IsRequired = true, SortOrder = 2, SectionName = "مشخصات اجاره" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_PropertyAddress", DisplayName = "آدرس ملک", FieldType = "Text", IsRequired = true, SortOrder = 3, SectionName = "مشخصات ملک" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_OwnerName", DisplayName = "نام مالک", FieldType = "Text", IsRequired = false, SortOrder = 4, SectionName = "مشخصات ملک" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_RentDueDay", DisplayName = "روز پرداخت اجاره", FieldType = "Number", IsRequired = true, SortOrder = 5, SectionName = "شرایط پرداخت" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_AnnualIncreasePercent", DisplayName = "درصد افزایش سالانه", FieldType = "Decimal", IsRequired = false, SortOrder = 6, SectionName = "شرایط پرداخت" });
        fields.Add(new() { ContractType = ContractTypes.Lease, FieldKey = "Lease_PaymentMethod", DisplayName = "روش پرداخت", FieldType = "Select", Options = "نقدی,چک,حواله,آنلاین", IsRequired = true, SortOrder = 7, SectionName = "شرایط پرداخت" });
    }

    private static void AddNdaFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.NDA, FieldKey = "NDA_DisclosurePurpose", DisplayName = "هدف از افشا", FieldType = "TextArea", IsRequired = true, SortOrder = 1, SectionName = "مشخصات محرمانگی" });
        fields.Add(new() { ContractType = ContractTypes.NDA, FieldKey = "NDA_ConfidentialityPeriod", DisplayName = "مدت محرمانگی (ماه)", FieldType = "Number", IsRequired = true, SortOrder = 2, SectionName = "مشخصات محرمانگی" });
        fields.Add(new() { ContractType = ContractTypes.NDA, FieldKey = "NDA_GoverningLaw", DisplayName = "قانون حاکم", FieldType = "Text", IsRequired = false, Placeholder = "مثلاً: قانون جمهوری اسلامی ایران", SortOrder = 3, SectionName = "شرایط قانونی" });
        fields.Add(new() { ContractType = ContractTypes.NDA, FieldKey = "NDA_IsMutual", DisplayName = "محرمانگی دوطرفه است؟", FieldType = "Boolean", IsRequired = true, SortOrder = 4, SectionName = "شرایط قانونی" });
    }

    private static void AddServiceFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Service, FieldKey = "Service_Scope", DisplayName = "حوزه / محدوده خدمات", FieldType = "TextArea", IsRequired = true, SortOrder = 1, SectionName = "مشخصات خدمات" });
        fields.Add(new() { ContractType = ContractTypes.Service, FieldKey = "Service_Deliverables", DisplayName = "تحویل‌دادنی‌ها", FieldType = "TextArea", IsRequired = false, SortOrder = 2, SectionName = "مشخصات خدمات" });
        fields.Add(new() { ContractType = ContractTypes.Service, FieldKey = "Service_ServiceFee", DisplayName = "حق‌الزحمه / مبلغ سرویس", FieldType = "Decimal", IsRequired = true, SortOrder = 3, SectionName = "مالی" });
        fields.Add(new() { ContractType = ContractTypes.Service, FieldKey = "Service_BillingCycle", DisplayName = "دوره صورتحساب", FieldType = "Select", Options = "یکبار,ماهانه,سه‌ماهه,شش‌ماهه,سالانه", IsRequired = true, SortOrder = 4, SectionName = "مالی" });
        fields.Add(new() { ContractType = ContractTypes.Service, FieldKey = "Service_ServiceLevel", DisplayName = "سطح خدمات", FieldType = "Select", Options = "پایه,نقره‌ای,طلایی,خاص", IsRequired = false, SortOrder = 5, SectionName = "سطح خدمات" });
    }

    private static void AddSalesFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Sales, FieldKey = "Sales_ProductName", DisplayName = "نام محصول/کالا", FieldType = "Text", IsRequired = true, SortOrder = 1, SectionName = "مشخصات فروش" });
        fields.Add(new() { ContractType = ContractTypes.Sales, FieldKey = "Sales_Quantity", DisplayName = "تعداد", FieldType = "Number", IsRequired = true, SortOrder = 2, SectionName = "مشخصات فروش" });
        fields.Add(new() { ContractType = ContractTypes.Sales, FieldKey = "Sales_UnitPrice", DisplayName = "قیمت واحد", FieldType = "Decimal", IsRequired = true, SortOrder = 3, SectionName = "مالی" });
        fields.Add(new() { ContractType = ContractTypes.Sales, FieldKey = "Sales_DeliveryDate", DisplayName = "تاریخ تحویل", FieldType = "Date", IsRequired = false, SortOrder = 4, SectionName = "تحویل" });
        fields.Add(new() { ContractType = ContractTypes.Sales, FieldKey = "Sales_DeliveryLocation", DisplayName = "مکان تحویل", FieldType = "Text", IsRequired = false, SortOrder = 5, SectionName = "تحویل" });
    }

    private static void AddPurchaseFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Purchase, FieldKey = "Purchase_SupplierName", DisplayName = "نام تأمین‌کننده", FieldType = "Text", IsRequired = true, SortOrder = 1, SectionName = "مشخصات خرید" });
        fields.Add(new() { ContractType = ContractTypes.Purchase, FieldKey = "Purchase_ItemDescription", DisplayName = "شرح کالا/خدمت", FieldType = "TextArea", IsRequired = true, SortOrder = 2, SectionName = "مشخصات خرید" });
        fields.Add(new() { ContractType = ContractTypes.Purchase, FieldKey = "Purchase_PaymentTerms", DisplayName = "شرایط پرداخت", FieldType = "Select", Options = "نقدی,نسیه,پیش‌پرداخت,اعتباری", IsRequired = true, SortOrder = 3, SectionName = "مالی" });
    }

    private static void AddPartnershipFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Partnership, FieldKey = "Partnership_PartnerName", DisplayName = "نام شریک", FieldType = "Text", IsRequired = true, SortOrder = 1, SectionName = "شرکا" });
        fields.Add(new() { ContractType = ContractTypes.Partnership, FieldKey = "Partnership_SharePercent", DisplayName = "درصد سهم", FieldType = "Decimal", IsRequired = true, SortOrder = 2, SectionName = "شرکا" });
        fields.Add(new() { ContractType = ContractTypes.Partnership, FieldKey = "Partnership_ContributionType", DisplayName = "نوع آورده", FieldType = "Select", Options = "نقدی,غیرنقدی,ترکیبی", IsRequired = true, SortOrder = 3, SectionName = "شرکا" });
        fields.Add(new() { ContractType = ContractTypes.Partnership, FieldKey = "Partnership_ProfitShareRatio", DisplayName = "نسبت تقسیم سود", FieldType = "Text", IsRequired = false, Placeholder = "مثلاً: 50:50", SortOrder = 4, SectionName = "مالی" });
    }

    private static void AddMaintenanceFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Maintenance, FieldKey = "Maint_AssetType", DisplayName = "نوع دارایی", FieldType = "Select", Options = "تجهیزات,ساختمان,خودرو,نرم‌افزار,سایر", IsRequired = true, SortOrder = 1, SectionName = "دارایی" });
        fields.Add(new() { ContractType = ContractTypes.Maintenance, FieldKey = "Maint_VisitFrequency", DisplayName = "دفعات بازدید", FieldType = "Select", Options = "هفتگی,ماهانه,سه‌ماهه,شش‌ماهه,سالانه", IsRequired = true, SortOrder = 2, SectionName = "برنامه" });
        fields.Add(new() { ContractType = ContractTypes.Maintenance, FieldKey = "Maint_ResponseTime_Hours", DisplayName = "زمان پاسخگویی (ساعت)", FieldType = "Number", IsRequired = true, SortOrder = 3, SectionName = "سطح خدمات" });
    }

    private static void AddConsultingFields(List<ContractTypeField> fields)
    {
        fields.Add(new() { ContractType = ContractTypes.Consulting, FieldKey = "Consult_EngagementType", DisplayName = "نوع همکاری", FieldType = "Select", Options = "پروژه‌ای,ساعتی,ماهانه,دوره‌ای", IsRequired = true, SortOrder = 1, SectionName = "نوع همکاری" });
        fields.Add(new() { ContractType = ContractTypes.Consulting, FieldKey = "Consult_HourlyRate", DisplayName = "نرخ ساعتی", FieldType = "Decimal", IsRequired = false, SortOrder = 2, SectionName = "مالی" });
        fields.Add(new() { ContractType = ContractTypes.Consulting, FieldKey = "Consult_TotalHours", DisplayName = "تعداد ساعات تخمینی", FieldType = "Number", IsRequired = false, SortOrder = 3, SectionName = "مالی" });
        fields.Add(new() { ContractType = ContractTypes.Consulting, FieldKey = "Consult_ProjectScope", DisplayName = "حوزه پروژه", FieldType = "TextArea", IsRequired = true, SortOrder = 4, SectionName = "مشخصات پروژه" });
    }
}