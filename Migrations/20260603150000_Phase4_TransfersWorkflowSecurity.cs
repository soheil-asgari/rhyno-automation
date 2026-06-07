using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    public partial class Phase4_TransfersWorkflowSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumStock",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentWorkflowStep",
                table: "Letters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Letters",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Letter");

            migrationBuilder.AddColumn<string>(
                name: "FinalReceiverId",
                table: "Letters",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWorkflowCompleted",
                table: "Letters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentManagerUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerUserId",
                table: "Warehouses",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceWarehouseId = table.Column<int>(type: "int", nullable: false),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransferRequests_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferRequests_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferRequests_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferRequests_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRoutes_AspNetUsers_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ParentManagerUserId",
                table: "AspNetUsers",
                column: "ParentManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_ApprovedByUserId",
                table: "InventoryTransferRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_DestinationWarehouseId",
                table: "InventoryTransferRequests",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_ProductId",
                table: "InventoryTransferRequests",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_RequestedByUserId",
                table: "InventoryTransferRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferRequests_SourceWarehouseId",
                table: "InventoryTransferRequests",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_FinalReceiverId",
                table: "Letters",
                column: "FinalReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionKey",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ManagerUserId",
                table: "Warehouses",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRoutes_ApproverUserId",
                table: "WorkflowRoutes",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRoutes_DocumentType_StepNumber",
                table: "WorkflowRoutes",
                columns: new[] { "DocumentType", "StepNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ParentManagerUserId",
                table: "AspNetUsers",
                column: "ParentManagerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Letters_AspNetUsers_FinalReceiverId",
                table: "Letters",
                column: "FinalReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_AspNetUsers_ManagerUserId",
                table: "Warehouses",
                column: "ManagerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ParentManagerUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Letters_AspNetUsers_FinalReceiverId",
                table: "Letters");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_AspNetUsers_ManagerUserId",
                table: "Warehouses");

            migrationBuilder.DropTable(
                name: "InventoryTransferRequests");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "WorkflowRoutes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ParentManagerUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Letters_FinalReceiverId",
                table: "Letters");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_ManagerUserId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MinimumStock",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CurrentWorkflowStep",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "FinalReceiverId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "IsWorkflowCompleted",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "ParentManagerUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ManagerUserId",
                table: "Warehouses");
        }
    }
}
