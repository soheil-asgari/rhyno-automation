using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddVoucherAuditAndMemoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMemo",
                table: "VoucherHeaders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "VoucherHeaders",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "IssuedBy",
                table: "VoucherHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedAt",
                table: "VoucherHeaders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "VoucherHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "VoucherHeaders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMemo",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "IssuedBy",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "VoucherHeaders");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "VoucherHeaders");
        }
    }
}
