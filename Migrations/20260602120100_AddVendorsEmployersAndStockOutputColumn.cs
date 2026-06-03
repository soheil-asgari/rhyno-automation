using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorsEmployersAndStockOutputColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "WarehouseReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployerId",
                table: "WarehouseIssuances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployerId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EconomicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseReceipts_VendorId",
                table: "WarehouseReceipts",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseIssuances_EmployerId",
                table: "WarehouseIssuances",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_EmployerId",
                table: "Invoices",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_ContractNumber",
                table: "Employers",
                column: "ContractNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_Name",
                table: "Employers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_EconomicCode",
                table: "Vendors",
                column: "EconomicCode");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Name",
                table: "Vendors",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Employers_EmployerId",
                table: "Invoices",
                column: "EmployerId",
                principalTable: "Employers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseIssuances_Employers_EmployerId",
                table: "WarehouseIssuances",
                column: "EmployerId",
                principalTable: "Employers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseReceipts_Vendors_VendorId",
                table: "WarehouseReceipts",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Employers_EmployerId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseIssuances_Employers_EmployerId",
                table: "WarehouseIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseReceipts_Vendors_VendorId",
                table: "WarehouseReceipts");

            migrationBuilder.DropTable(
                name: "Employers");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseReceipts_VendorId",
                table: "WarehouseReceipts");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseIssuances_EmployerId",
                table: "WarehouseIssuances");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_EmployerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "WarehouseReceipts");

            migrationBuilder.DropColumn(
                name: "EmployerId",
                table: "WarehouseIssuances");

            migrationBuilder.DropColumn(
                name: "EmployerId",
                table: "Invoices");
        }
    }
}
