using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVersioningTaskTimeLogAndTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "SystemSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                table: "SystemSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginBackgroundUrl",
                table: "SystemSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "SystemSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "SystemSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentVersion",
                table: "DocumentArchiveItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "DocumentArchiveItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "DocumentArchiveItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodes",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecretKey",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentArchiveItemId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_DocumentArchiveItems_DocumentArchiveItemId",
                        column: x => x.DocumentArchiveItemId,
                        principalTable: "DocumentArchiveItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTimeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowCaseTaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HoursSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTimeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTimeLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskTimeLogs_WorkflowCaseTasks_WorkflowCaseTaskId",
                        column: x => x.WorkflowCaseTaskId,
                        principalTable: "WorkflowCaseTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentArchiveItems_ExpirationDate",
                table: "DocumentArchiveItems",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentArchiveItems_IsArchived",
                table: "DocumentArchiveItems",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_CreatedByUserId",
                table: "DocumentVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentArchiveItemId",
                table: "DocumentVersions",
                column: "DocumentArchiveItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeLogs_LoggedAt",
                table: "TaskTimeLogs",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeLogs_UserId",
                table: "TaskTimeLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeLogs_WorkflowCaseTaskId",
                table: "TaskTimeLogs",
                column: "WorkflowCaseTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "TaskTimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_DocumentArchiveItems_ExpirationDate",
                table: "DocumentArchiveItems");

            migrationBuilder.DropIndex(
                name: "IX_DocumentArchiveItems_IsArchived",
                table: "DocumentArchiveItems");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoginBackgroundUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CurrentVersion",
                table: "DocumentArchiveItems");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "DocumentArchiveItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "DocumentArchiveItems");

            migrationBuilder.DropColumn(
                name: "RecoveryCodes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecretKey",
                table: "AspNetUsers");
        }
    }
}
