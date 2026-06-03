using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTrackingAndWarehouseMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeadlineDateShamsi",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpEmployeeId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseReceiptId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_FollowUpEmployeeId",
                table: "Invoices",
                column: "FollowUpEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WarehouseReceiptId",
                table: "Invoices",
                column: "WarehouseReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_HumanCapitalEmployees_FollowUpEmployeeId",
                table: "Invoices",
                column: "FollowUpEmployeeId",
                principalTable: "HumanCapitalEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_WarehouseReceipts_WarehouseReceiptId",
                table: "Invoices",
                column: "WarehouseReceiptId",
                principalTable: "WarehouseReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_HumanCapitalEmployees_FollowUpEmployeeId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_WarehouseReceipts_WarehouseReceiptId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_FollowUpEmployeeId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_WarehouseReceiptId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeadlineDateShamsi",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FollowUpEmployeeId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WarehouseReceiptId",
                table: "Invoices");
        }
    }
}
