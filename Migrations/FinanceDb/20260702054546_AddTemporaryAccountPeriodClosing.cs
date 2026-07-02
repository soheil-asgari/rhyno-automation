using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddTemporaryAccountPeriodClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemporary",
                table: "SubsidiaryAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE sa
                SET sa.IsTemporary = 1
                FROM SubsidiaryAccounts sa
                INNER JOIN GeneralAccounts ga ON ga.Id = sa.GeneralAccountId
                INNER JOIN AccountGroups ag ON ag.Id = ga.AccountGroupId
                WHERE ag.Code IN ('4', '5');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemporary",
                table: "SubsidiaryAccounts");
        }
    }
}
