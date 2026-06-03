using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateInsurancePayrollWithHumanCapital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HumanCapitalEmployeeId",
                table: "PayrollItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HumanCapitalEmployeeId",
                table: "InsuranceEmployees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_HumanCapitalEmployeeId",
                table: "PayrollItems",
                column: "HumanCapitalEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceEmployees_HumanCapitalEmployeeId",
                table: "InsuranceEmployees",
                column: "HumanCapitalEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InsuranceEmployees_HumanCapitalEmployees_HumanCapitalEmployeeId",
                table: "InsuranceEmployees",
                column: "HumanCapitalEmployeeId",
                principalTable: "HumanCapitalEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollItems_HumanCapitalEmployees_HumanCapitalEmployeeId",
                table: "PayrollItems",
                column: "HumanCapitalEmployeeId",
                principalTable: "HumanCapitalEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InsuranceEmployees_HumanCapitalEmployees_HumanCapitalEmployeeId",
                table: "InsuranceEmployees");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollItems_HumanCapitalEmployees_HumanCapitalEmployeeId",
                table: "PayrollItems");

            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_HumanCapitalEmployeeId",
                table: "PayrollItems");

            migrationBuilder.DropIndex(
                name: "IX_InsuranceEmployees_HumanCapitalEmployeeId",
                table: "InsuranceEmployees");

            migrationBuilder.DropColumn(
                name: "HumanCapitalEmployeeId",
                table: "PayrollItems");

            migrationBuilder.DropColumn(
                name: "HumanCapitalEmployeeId",
                table: "InsuranceEmployees");
        }
    }
}
