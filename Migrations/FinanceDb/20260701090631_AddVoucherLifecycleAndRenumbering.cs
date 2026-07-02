using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    public partial class AddVoucherLifecycleAndRenumbering : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE VoucherHeaders
                SET Status = CASE Status
                    WHEN 'Posted' THEN 'Permanent'
                    WHEN 'Temporary' THEN 'Reviewed'
                    WHEN 'Approved' THEN 'Approved'
                    ELSE 'Draft'
                END
                """);

            migrationBuilder.DropIndex(
                name: "IX_VoucherHeaders_VoucherNumber",
                table: "VoucherHeaders");

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "VoucherHeaders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VoucherNumberInt",
                table: "VoucherHeaders",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                ;WITH OrderedVouchers AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY FiscalYearId ORDER BY VoucherDate, Id) AS SequenceValue
                    FROM VoucherHeaders
                )
                UPDATE vh
                SET
                    vh.SequenceNumber = ov.SequenceValue,
                    vh.VoucherNumberInt = TRY_CONVERT(int, vh.VoucherNumber)
                FROM VoucherHeaders vh
                INNER JOIN OrderedVouchers ov ON ov.Id = vh.Id
                """);

            migrationBuilder.DropColumn(
                name: "VoucherNumber",
                table: "VoucherHeaders");

            migrationBuilder.RenameColumn(
                name: "VoucherNumberInt",
                table: "VoucherHeaders",
                newName: "VoucherNumber");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherHeaders_FiscalYearId_SequenceNumber",
                table: "VoucherHeaders",
                columns: new[] { "FiscalYearId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherHeaders_FiscalYearId_VoucherNumber",
                table: "VoucherHeaders",
                columns: new[] { "FiscalYearId", "VoucherNumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherHeaders_FiscalYearId_SequenceNumber",
                table: "VoucherHeaders");

            migrationBuilder.DropIndex(
                name: "IX_VoucherHeaders_FiscalYearId_VoucherNumber",
                table: "VoucherHeaders");

            migrationBuilder.RenameColumn(
                name: "VoucherNumber",
                table: "VoucherHeaders",
                newName: "VoucherNumberInt");

            migrationBuilder.AddColumn<string>(
                name: "VoucherNumber",
                table: "VoucherHeaders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE VoucherHeaders
                SET VoucherNumber = CONVERT(nvarchar(50), VoucherNumberInt)
                """);

            migrationBuilder.DropColumn(
                name: "VoucherNumberInt",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "VoucherHeaders");

            migrationBuilder.Sql("""
                UPDATE VoucherHeaders
                SET Status = CASE Status
                    WHEN 'Permanent' THEN 'Posted'
                    WHEN 'Reviewed' THEN 'Temporary'
                    WHEN 'Approved' THEN 'Approved'
                    ELSE 'Draft'
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherHeaders_VoucherNumber",
                table: "VoucherHeaders",
                column: "VoucherNumber",
                unique: true);
        }
    }
}
