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


        public DbSet<InsuranceEmployee> InsuranceEmployees { get; set; }
        public DbSet<Department> Departments { get; set; }



        public DbSet<Letter> Letters { get; set; }
        public DbSet<Leave> Leaves { get; set; }

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
        }

    }
}