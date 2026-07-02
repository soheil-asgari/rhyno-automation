using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddTrialBalanceReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_SubsidiaryAccountId_FloatingDetailAccountId",
                table: "VoucherLines",
                columns: new[] { "SubsidiaryAccountId", "FloatingDetailAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherHeaders_Status_VoucherDate_TotalDebits_TotalCredits",
                table: "VoucherHeaders",
                columns: new[] { "Status", "VoucherDate", "TotalDebits", "TotalCredits" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherHeaders_VoucherDate",
                table: "VoucherHeaders",
                column: "VoucherDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherLines_SubsidiaryAccountId_FloatingDetailAccountId",
                table: "VoucherLines");

            migrationBuilder.DropIndex(
                name: "IX_VoucherHeaders_Status_VoucherDate_TotalDebits_TotalCredits",
                table: "VoucherHeaders");

            migrationBuilder.DropIndex(
                name: "IX_VoucherHeaders_VoucherDate",
                table: "VoucherHeaders");
        }
    }
}
