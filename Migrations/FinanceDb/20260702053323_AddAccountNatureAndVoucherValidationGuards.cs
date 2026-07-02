using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddAccountNatureAndVoucherValidationGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nature",
                table: "SubsidiaryAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoControl");

            migrationBuilder.AddColumn<string>(
                name: "Nature",
                table: "GeneralAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoControl");

            migrationBuilder.Sql(
                """
                UPDATE ga
                SET ga.Nature = COALESCE(NULLIF(ag.Nature, ''), 'NoControl')
                FROM GeneralAccounts ga
                INNER JOIN AccountGroups ag ON ag.Id = ga.AccountGroupId;
                """);

            migrationBuilder.Sql(
                """
                UPDATE sa
                SET sa.Nature = COALESCE(NULLIF(ga.Nature, ''), 'NoControl')
                FROM SubsidiaryAccounts sa
                INNER JOIN GeneralAccounts ga ON ga.Id = sa.GeneralAccountId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nature",
                table: "SubsidiaryAccounts");

            migrationBuilder.DropColumn(
                name: "Nature",
                table: "GeneralAccounts");
        }
    }
}
