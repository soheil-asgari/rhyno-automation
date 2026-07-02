using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddFloatingDetailAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FloatingDetailAccountId",
                table: "VoucherLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FloatingDetailAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloatingDetailAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubsidiaryAccountFloatingDetails",
                columns: table => new
                {
                    SubsidiaryAccountId = table.Column<int>(type: "int", nullable: false),
                    FloatingDetailAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubsidiaryAccountFloatingDetails", x => new { x.SubsidiaryAccountId, x.FloatingDetailAccountId });
                    table.ForeignKey(
                        name: "FK_SubsidiaryAccountFloatingDetails_FloatingDetailAccounts_FloatingDetailAccountId",
                        column: x => x.FloatingDetailAccountId,
                        principalTable: "FloatingDetailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubsidiaryAccountFloatingDetails_SubsidiaryAccounts_SubsidiaryAccountId",
                        column: x => x.SubsidiaryAccountId,
                        principalTable: "SubsidiaryAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_FloatingDetailAccountId",
                table: "VoucherLines",
                column: "FloatingDetailAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatingDetailAccounts_Code",
                table: "FloatingDetailAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FloatingDetailAccounts_Type_Name",
                table: "FloatingDetailAccounts",
                columns: new[] { "Type", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SubsidiaryAccountFloatingDetails_FloatingDetailAccountId",
                table: "SubsidiaryAccountFloatingDetails",
                column: "FloatingDetailAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherLines_FloatingDetailAccounts_FloatingDetailAccountId",
                table: "VoucherLines",
                column: "FloatingDetailAccountId",
                principalTable: "FloatingDetailAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoucherLines_FloatingDetailAccounts_FloatingDetailAccountId",
                table: "VoucherLines");

            migrationBuilder.DropTable(
                name: "SubsidiaryAccountFloatingDetails");

            migrationBuilder.DropTable(
                name: "FloatingDetailAccounts");

            migrationBuilder.DropIndex(
                name: "IX_VoucherLines_FloatingDetailAccountId",
                table: "VoucherLines");

            migrationBuilder.DropColumn(
                name: "FloatingDetailAccountId",
                table: "VoucherLines");
        }
    }
}
