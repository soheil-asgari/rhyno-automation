using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class ApplyConcurrencyAndFixUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoice_Number_Vendor",
                table: "Invoices");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PayrollLists",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Invoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryStocks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Number_Type",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "InvoiceType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoice_Number_Type",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PayrollLists");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryStocks");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Number_Vendor",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "VendorName" },
                unique: true);
        }
    }
}
