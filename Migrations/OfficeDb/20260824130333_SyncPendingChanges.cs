using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class SyncPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentVersionId",
                table: "ContractAttachments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "ContractAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContractAttachmentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ChangeDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAttachmentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAttachmentVersions_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAttachmentVersions_ContractAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "ContractAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachments_CurrentVersionId",
                table: "ContractAttachments",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachmentVersions_AttachmentId",
                table: "ContractAttachmentVersions",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachmentVersions_AttachmentId_VersionNumber",
                table: "ContractAttachmentVersions",
                columns: new[] { "AttachmentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachmentVersions_UploadedByUserId",
                table: "ContractAttachmentVersions",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments",
                column: "CurrentVersionId",
                principalTable: "ContractAttachmentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments");

            migrationBuilder.DropTable(
                name: "ContractAttachmentVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContractAttachments_CurrentVersionId",
                table: "ContractAttachments");

            migrationBuilder.DropColumn(
                name: "CurrentVersionId",
                table: "ContractAttachments");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "ContractAttachments");
        }
    }
}
