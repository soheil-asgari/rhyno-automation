using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddJournalTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    JournalTypeId = table.Column<int>(type: "int", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RecurrenceEveryMonths = table.Column<int>(type: "int", nullable: true),
                    NextRunDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalTemplates_JournalTypes_JournalTypeId",
                        column: x => x.JournalTypeId,
                        principalTable: "JournalTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalTemplateLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalTemplateId = table.Column<int>(type: "int", nullable: false),
                    SubsidiaryAccountId = table.Column<int>(type: "int", nullable: false),
                    FloatingDetailAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalTemplateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalTemplateLines_FloatingDetailAccounts_FloatingDetailAccountId",
                        column: x => x.FloatingDetailAccountId,
                        principalTable: "FloatingDetailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalTemplateLines_JournalTemplates_JournalTemplateId",
                        column: x => x.JournalTemplateId,
                        principalTable: "JournalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalTemplateLines_SubsidiaryAccounts_SubsidiaryAccountId",
                        column: x => x.SubsidiaryAccountId,
                        principalTable: "SubsidiaryAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalTemplateLines_FloatingDetailAccountId",
                table: "JournalTemplateLines",
                column: "FloatingDetailAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTemplateLines_JournalTemplateId",
                table: "JournalTemplateLines",
                column: "JournalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTemplateLines_SubsidiaryAccountId",
                table: "JournalTemplateLines",
                column: "SubsidiaryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTemplates_JournalTypeId",
                table: "JournalTemplates",
                column: "JournalTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTemplates_Name",
                table: "JournalTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalTemplateLines");

            migrationBuilder.DropTable(
                name: "JournalTemplates");
        }
    }
}
