using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsolationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "OutboxMessages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Notifications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_Status_LockedUntil_OccurredAt",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "Status", "LockedUntil", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_RecipientUserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "RecipientUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_DateTime",
                table: "AuditLogs",
                columns: new[] { "TenantId", "DateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_TenantId_Status_LockedUntil_OccurredAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_RecipientUserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId_DateTime",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");
        }
    }
}

