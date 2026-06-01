using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;

namespace OfficeAutomation.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<InsuranceList> InsuranceLists { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Waybill> Waybills { get; set; }
        public DbSet<InsuranceEmployee> InsuranceEmployees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<HumanCapitalEmployee> HumanCapitalEmployees { get; set; }
        public DbSet<HumanCapitalSalaryHistory> HumanCapitalSalaryHistories { get; set; }
        public DbSet<HumanCapitalStatusHistory> HumanCapitalStatusHistories { get; set; }
        public DbSet<Letter> Letters { get; set; }
        public DbSet<Leave> Leaves { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Letter>()
                .HasOne(l => l.Sender)
                .WithMany()
                .HasForeignKey(l => l.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Letter>()
                .HasOne(l => l.Receiver)
                .WithMany()
                .HasForeignKey(l => l.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.Manager)
                .WithMany()
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "Financial" },
                new Department { Id = 2, Name = "Administrative" },
                new Department { Id = 3, Name = "Technical" },
                new Department { Id = 4, Name = "HR" },
                new Department { Id = 5, Name = "Management" }
            );

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => new { i.InvoiceNumber, i.VendorName })
                .IsUnique()
                .HasDatabaseName("IX_Invoice_Number_Vendor");

            modelBuilder.Entity<Waybill>()
                .HasIndex(waybill => waybill.WaybillNumber)
                .IsUnique();

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.Weight)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.TotalFreightCharges)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.DriverCommission)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.NetPayToDriver)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasOne(employee => employee.Department)
                .WithMany()
                .HasForeignKey(employee => employee.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasIndex(employee => employee.PersonnelCode)
                .IsUnique();

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasIndex(employee => employee.NationalCode)
                .IsUnique();

            modelBuilder.Entity<HumanCapitalEmployee>()
                .Property(employee => employee.CurrentSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .HasOne(history => history.Employee)
                .WithMany(employee => employee.SalaryHistories)
                .HasForeignKey(history => history.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .Property(history => history.PreviousSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .Property(history => history.NewSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalStatusHistory>()
                .HasOne(history => history.Employee)
                .WithMany(employee => employee.StatusHistories)
                .HasForeignKey(history => history.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HumanCapitalStatusHistory>()
                .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(setting => setting.ApplicationTitle);

            modelBuilder.Entity<UserPreference>()
                .HasIndex(preference => preference.UserId)
                .IsUnique();

            modelBuilder.Entity<UserPreference>()
                .HasOne(preference => preference.User)
                .WithMany()
                .HasForeignKey(preference => preference.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
