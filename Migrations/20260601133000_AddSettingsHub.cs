using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SystemLanguage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActiveEnvironment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MaintenanceMode = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SidebarCollapsedByDefault = table.Column<bool>(type: "bit", nullable: false),
                    ThemePreference = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_ApplicationTitle",
                table: "SystemSettings",
                column: "ApplicationTitle");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
