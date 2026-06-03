using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class WarehouseExpansionAndClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "WarehouseReceipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "WarehouseIssuances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "InventoryCountings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseClosings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosingDateShamsi = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClosingYear = table.Column<int>(type: "int", nullable: false),
                    OpeningYear = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseClosings_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryOpeningBalanceLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseClosingId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryOpeningBalanceLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryOpeningBalanceLedgers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryOpeningBalanceLedgers_WarehouseClosings_WarehouseClosingId",
                        column: x => x.WarehouseClosingId,
                        principalTable: "WarehouseClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryOpeningBalanceLedgers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseClosingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseClosingId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ClosingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OpeningQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseClosingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseClosingItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseClosingItems_WarehouseClosings_WarehouseClosingId",
                        column: x => x.WarehouseClosingId,
                        principalTable: "WarehouseClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Warehouses",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "IsClosed", "Location", "Name" },
                values: new object[] { 1, "WH-MAIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "ستاد", "انبار مرکزی" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseReceipts_WarehouseId",
                table: "WarehouseReceipts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseIssuances_WarehouseId",
                table: "WarehouseIssuances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_WarehouseId",
                table: "InventoryStocks",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountings_WarehouseId",
                table: "InventoryCountings",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOpeningBalanceLedgers_ProductId",
                table: "InventoryOpeningBalanceLedgers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOpeningBalanceLedgers_WarehouseClosingId",
                table: "InventoryOpeningBalanceLedgers",
                column: "WarehouseClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOpeningBalanceLedgers_WarehouseId_ProductId_PeriodYear",
                table: "InventoryOpeningBalanceLedgers",
                columns: new[] { "WarehouseId", "ProductId", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseClosingItems_ProductId",
                table: "WarehouseClosingItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseClosingItems_WarehouseClosingId",
                table: "WarehouseClosingItems",
                column: "WarehouseClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseClosings_DocumentNumber",
                table: "WarehouseClosings",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseClosings_WarehouseId",
                table: "WarehouseClosings",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCountings_Warehouses_WarehouseId",
                table: "InventoryCountings",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStocks_Warehouses_WarehouseId",
                table: "InventoryStocks",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseIssuances_Warehouses_WarehouseId",
                table: "WarehouseIssuances",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseReceipts_Warehouses_WarehouseId",
                table: "WarehouseReceipts",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCountings_Warehouses_WarehouseId",
                table: "InventoryCountings");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryStocks_Warehouses_WarehouseId",
                table: "InventoryStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseIssuances_Warehouses_WarehouseId",
                table: "WarehouseIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseReceipts_Warehouses_WarehouseId",
                table: "WarehouseReceipts");

            migrationBuilder.DropTable(
                name: "InventoryOpeningBalanceLedgers");

            migrationBuilder.DropTable(
                name: "WarehouseClosingItems");

            migrationBuilder.DropTable(
                name: "WarehouseClosings");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseReceipts_WarehouseId",
                table: "WarehouseReceipts");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseIssuances_WarehouseId",
                table: "WarehouseIssuances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStocks_WarehouseId",
                table: "InventoryStocks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCountings_WarehouseId",
                table: "InventoryCountings");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "WarehouseReceipts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "WarehouseIssuances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "InventoryCountings");
        }
    }
}
