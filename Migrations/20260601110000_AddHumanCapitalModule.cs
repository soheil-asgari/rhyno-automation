using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanCapitalModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HumanCapitalEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonnelCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NationalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OnboardingCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    PositionTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CurrentSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanCapitalEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HumanCapitalEmployees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HumanCapitalSalaryHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PromotionTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanCapitalSalaryHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HumanCapitalSalaryHistories_HumanCapitalEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HumanCapitalEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HumanCapitalStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StatusType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExitReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanCapitalStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HumanCapitalStatusHistories_HumanCapitalEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HumanCapitalEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HumanCapitalEmployees_DepartmentId",
                table: "HumanCapitalEmployees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HumanCapitalEmployees_NationalCode",
                table: "HumanCapitalEmployees",
                column: "NationalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HumanCapitalEmployees_PersonnelCode",
                table: "HumanCapitalEmployees",
                column: "PersonnelCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HumanCapitalSalaryHistories_EmployeeId_EffectiveDate",
                table: "HumanCapitalSalaryHistories",
                columns: new[] { "EmployeeId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HumanCapitalStatusHistories_EmployeeId_EffectiveDate",
                table: "HumanCapitalStatusHistories",
                columns: new[] { "EmployeeId", "EffectiveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HumanCapitalSalaryHistories");

            migrationBuilder.DropTable(
                name: "HumanCapitalStatusHistories");

            migrationBuilder.DropTable(
                name: "HumanCapitalEmployees");
        }
    }
}
