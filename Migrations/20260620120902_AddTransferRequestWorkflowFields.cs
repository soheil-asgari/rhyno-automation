using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferRequestWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "InventoryTransferRequests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceledAt",
                table: "InventoryTransferRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceledByUserId",
                table: "InventoryTransferRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "InventoryTransferRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "InventoryTransferRequests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "InventoryTransferRequests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "InventoryTransferRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByUserId",
                table: "InventoryTransferRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryMovementLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    QuantityIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityOut = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovementLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovementLedgers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryMovementLedgers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_CanceledByUserId",
                table: "InventoryTransferRequests",
                column: "CanceledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_RejectedByUserId",
                table: "InventoryTransferRequests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLedgers_ProductId",
                table: "InventoryMovementLedgers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLedgers_WarehouseId",
                table: "InventoryMovementLedgers",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferRequests_AspNetUsers_CanceledByUserId",
                table: "InventoryTransferRequests",
                column: "CanceledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferRequests_AspNetUsers_RejectedByUserId",
                table: "InventoryTransferRequests",
                column: "RejectedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferRequests_AspNetUsers_CanceledByUserId",
                table: "InventoryTransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferRequests_AspNetUsers_RejectedByUserId",
                table: "InventoryTransferRequests");

            migrationBuilder.DropTable(
                name: "InventoryMovementLedgers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransferRequests_CanceledByUserId",
                table: "InventoryTransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransferRequests_RejectedByUserId",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "CanceledAt",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "CanceledByUserId",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "InventoryTransferRequests");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "InventoryTransferRequests");
        }
    }
}
