using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class CLM_ContractManagement_Enhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ArchivedByUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ResponsibleUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractTemplates_ContractTemplateId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractTemplates_AspNetUsers_UserId",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_UserId",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_UserId_IsDefault",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ExpirationDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "FormatJson",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RenewalDate",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "TemplateHtml",
                table: "ContractTemplates",
                newName: "HtmlContent");

            migrationBuilder.RenameColumn(
                name: "SigningDate",
                table: "Contracts",
                newName: "SignDate");

            migrationBuilder.RenameColumn(
                name: "ResponsibleUserId",
                table: "Contracts",
                newName: "AssignedUserId");

            migrationBuilder.RenameColumn(
                name: "ContractTemplateId",
                table: "Contracts",
                newName: "TemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ResponsibleUserId",
                table: "Contracts",
                newName: "IX_Contracts_AssignedUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ContractTemplateId",
                table: "Contracts",
                newName: "IX_Contracts_TemplateId");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "ContractPayments",
                newName: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "ContractTemplates",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                table: "ContractTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "ntext",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractBodyHtml",
                table: "Contracts",
                type: "ntext",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryNoticeDays",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "ContractPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ContractPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingBalance",
                table: "Contracts",
                type: "decimal(18,2)",
                nullable: false,
                computedColumnSql: "TotalAmount - PaidAmount");

            migrationBuilder.CreateTable(
                name: "ContractAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAuditLogs_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_OwnerUserId",
                table: "ContractTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_OwnerUserId_IsDefault",
                table: "ContractTemplates",
                columns: new[] { "OwnerUserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLogs_ContractId",
                table: "ContractAuditLogs",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLogs_UserId",
                table: "ContractAuditLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ArchivedByUserId",
                table: "Contracts",
                column: "ArchivedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_AssignedUserId",
                table: "Contracts",
                column: "AssignedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractTemplates_TemplateId",
                table: "Contracts",
                column: "TemplateId",
                principalTable: "ContractTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTemplates_AspNetUsers_OwnerUserId",
                table: "ContractTemplates",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ArchivedByUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_AssignedUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractTemplates_TemplateId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractTemplates_AspNetUsers_OwnerUserId",
                table: "ContractTemplates");

            migrationBuilder.DropTable(
                name: "ContractAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_OwnerUserId",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_OwnerUserId_IsDefault",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "RemainingBalance",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "ContractBodyHtml",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ExpiryNoticeDays",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ContractPayments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ContractPayments");

            migrationBuilder.RenameColumn(
                name: "HtmlContent",
                table: "ContractTemplates",
                newName: "TemplateHtml");

            migrationBuilder.RenameColumn(
                name: "TemplateId",
                table: "Contracts",
                newName: "ContractTemplateId");

            migrationBuilder.RenameColumn(
                name: "SignDate",
                table: "Contracts",
                newName: "SigningDate");

            migrationBuilder.RenameColumn(
                name: "AssignedUserId",
                table: "Contracts",
                newName: "ResponsibleUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_TemplateId",
                table: "Contracts",
                newName: "IX_Contracts_ContractTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_AssignedUserId",
                table: "Contracts",
                newName: "IX_Contracts_ResponsibleUserId");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "ContractPayments",
                newName: "Description");

            migrationBuilder.AddColumn<string>(
                name: "FormatJson",
                table: "ContractTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ContractTemplates",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Contracts",
                type: "ntext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalDate",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_UserId",
                table: "ContractTemplates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_UserId_IsDefault",
                table: "ContractTemplates",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ExpirationDate",
                table: "Contracts",
                column: "ExpirationDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ArchivedByUserId",
                table: "Contracts",
                column: "ArchivedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ResponsibleUserId",
                table: "Contracts",
                column: "ResponsibleUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractTemplates_ContractTemplateId",
                table: "Contracts",
                column: "ContractTemplateId",
                principalTable: "ContractTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTemplates_AspNetUsers_UserId",
                table: "ContractTemplates",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
