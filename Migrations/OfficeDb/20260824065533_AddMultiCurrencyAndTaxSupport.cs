using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class AddMultiCurrencyAndTaxSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseCurrencyAmount",
                table: "Contracts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Contracts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "IRR");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultTaxRatePercent",
                table: "Contracts",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Contracts",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "TaxMethod",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Exclusive");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountInclusiveTax",
                table: "Contracts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTaxAmount",
                table: "Contracts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "ContractPayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "ContractPayments",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "ContractPayments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "ContractAmendments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "ContractAmendments",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRatePercent",
                table: "ContractAmendments",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractTaxLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    TaxName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxBaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculationMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaxCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsExempt = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AmendmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTaxLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTaxLines_ContractAmendments_AmendmentId",
                        column: x => x.AmendmentId,
                        principalTable: "ContractAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContractTaxLines_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CurrencyCode",
                table: "Contracts",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TaxMethod",
                table: "Contracts",
                column: "TaxMethod");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTaxLines_AmendmentId",
                table: "ContractTaxLines",
                column: "AmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTaxLines_ContractId",
                table: "ContractTaxLines",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTaxLines_TaxCategory",
                table: "ContractTaxLines",
                column: "TaxCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTaxLines");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CurrencyCode",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_TaxMethod",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DefaultTaxRatePercent",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TaxMethod",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TotalAmountInclusiveTax",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TotalTaxAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "ContractPayments");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "ContractPayments");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "ContractPayments");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "ContractAmendments");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "ContractAmendments");

            migrationBuilder.DropColumn(
                name: "TaxRatePercent",
                table: "ContractAmendments");
        }
    }
}
