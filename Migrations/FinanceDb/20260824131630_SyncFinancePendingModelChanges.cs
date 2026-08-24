using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class SyncFinancePendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationJournalTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceSubsidiaryAccountId = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JournalTypeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationJournalTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationJournalTemplate_JournalTypes_JournalTypeId",
                        column: x => x.JournalTypeId,
                        principalTable: "JournalTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationJournalTemplate_SubsidiaryAccounts_SourceSubsidiaryAccountId",
                        column: x => x.SourceSubsidiaryAccountId,
                        principalTable: "SubsidiaryAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriodAuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriodAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalPeriodAuditLog_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AllocationJournalTemplateLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllocationJournalTemplateId = table.Column<int>(type: "int", nullable: false),
                    TargetSubsidiaryAccountId = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    Ratio = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationJournalTemplateLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationJournalTemplateLine_AllocationJournalTemplate_AllocationJournalTemplateId",
                        column: x => x.AllocationJournalTemplateId,
                        principalTable: "AllocationJournalTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllocationJournalTemplateLine_SubsidiaryAccounts_TargetSubsidiaryAccountId",
                        column: x => x.TargetSubsidiaryAccountId,
                        principalTable: "SubsidiaryAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationJournalTemplate_JournalTypeId",
                table: "AllocationJournalTemplate",
                column: "JournalTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationJournalTemplate_SourceSubsidiaryAccountId",
                table: "AllocationJournalTemplate",
                column: "SourceSubsidiaryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationJournalTemplateLine_AllocationJournalTemplateId",
                table: "AllocationJournalTemplateLine",
                column: "AllocationJournalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationJournalTemplateLine_TargetSubsidiaryAccountId",
                table: "AllocationJournalTemplateLine",
                column: "TargetSubsidiaryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriodAuditLog_FiscalPeriodId",
                table: "FiscalPeriodAuditLog",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriodAuditLog_Timestamp",
                table: "FiscalPeriodAuditLog",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationJournalTemplateLine");

            migrationBuilder.DropTable(
                name: "FiscalPeriodAuditLog");

            migrationBuilder.DropTable(
                name: "AllocationJournalTemplate");
        }
    }
}
