using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20260630124500_AddDynamicTenantRegistry")]
    public partial class AddDynamicTenantRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantDefinitions",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsolationMode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DatabaseSchema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    QueueNamespace = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CachePrefix = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StorageRoot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LogPrefix = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LogRoot = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SettingsNamespace = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    JobNamespace = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RedisDatabase = table.Column<int>(type: "int", nullable: true),
                    EnableOutboxPublisher = table.Column<bool>(type: "bit", nullable: false),
                    EnableWorkflowJobs = table.Column<bool>(type: "bit", nullable: false),
                    RequestsPerMinute = table.Column<int>(type: "int", nullable: false),
                    AiRequestsPerMinute = table.Column<int>(type: "int", nullable: false),
                    MaxActiveUsers = table.Column<int>(type: "int", nullable: true),
                    MaxStorageMegabytes = table.Column<long>(type: "bigint", nullable: true),
                    MaxWorkflowInstances = table.Column<int>(type: "int", nullable: true),
                    MaxStorageFiles = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDefinitions", x => x.TenantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantDefinitions_LifecycleState",
                table: "TenantDefinitions",
                column: "LifecycleState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDefinitions");
        }
    }
}
