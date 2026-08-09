using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.TaxPersistence
{
    /// <inheritdoc />
    public partial class AddMowadiyanPortalSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortalPassword",
                table: "MowadiyanSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalUsername",
                table: "MowadiyanSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsMowadiyanSynced",
                table: "InvoiceRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortalPassword",
                table: "MowadiyanSettings");

            migrationBuilder.DropColumn(
                name: "PortalUsername",
                table: "MowadiyanSettings");

            migrationBuilder.DropColumn(
                name: "IsMowadiyanSynced",
                table: "InvoiceRecords");
        }
    }
}
