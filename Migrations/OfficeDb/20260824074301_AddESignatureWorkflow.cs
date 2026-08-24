using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class AddESignatureWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractSignatureRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    AmendmentId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SigningOrder = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LinkExpiryHours = table.Column<int>(type: "int", nullable: true),
                    LinkExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AutoActivateContract = table.Column<bool>(type: "bit", nullable: false),
                    CurrentSignerStep = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSignatureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSignatureRequests_ContractAmendments_AmendmentId",
                        column: x => x.AmendmentId,
                        principalTable: "ContractAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContractSignatureRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractSignatureSigners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignatureRequestId = table.Column<int>(type: "int", nullable: false),
                    SignerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignOrder = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignerToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    SignedUserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CertificateThumbprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSignatureSigners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSignatureSigners_AspNetUsers_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractSignatureSigners_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractSignatureSigners_ContractSignatureRequests_SignatureRequestId",
                        column: x => x.SignatureRequestId,
                        principalTable: "ContractSignatureRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractSignatureAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignatureRequestId = table.Column<int>(type: "int", nullable: false),
                    SignerId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSignatureAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSignatureAuditLogs_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractSignatureAuditLogs_ContractSignatureRequests_SignatureRequestId",
                        column: x => x.SignatureRequestId,
                        principalTable: "ContractSignatureRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractSignatureAuditLogs_ContractSignatureSigners_SignerId",
                        column: x => x.SignerId,
                        principalTable: "ContractSignatureSigners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureAuditLogs_EventType",
                table: "ContractSignatureAuditLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureAuditLogs_PerformedByUserId",
                table: "ContractSignatureAuditLogs",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureAuditLogs_SignatureRequestId",
                table: "ContractSignatureAuditLogs",
                column: "SignatureRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureAuditLogs_SignerId",
                table: "ContractSignatureAuditLogs",
                column: "SignerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRequests_AccessToken",
                table: "ContractSignatureRequests",
                column: "AccessToken",
                unique: true,
                filter: "[AccessToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRequests_AmendmentId",
                table: "ContractSignatureRequests",
                column: "AmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRequests_ContractId",
                table: "ContractSignatureRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRequests_Status",
                table: "ContractSignatureRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureSigners_Result",
                table: "ContractSignatureSigners",
                column: "Result");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureSigners_SentByUserId",
                table: "ContractSignatureSigners",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureSigners_SignatureRequestId",
                table: "ContractSignatureSigners",
                column: "SignatureRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureSigners_SignerToken",
                table: "ContractSignatureSigners",
                column: "SignerToken",
                unique: true,
                filter: "[SignerToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureSigners_UserId",
                table: "ContractSignatureSigners",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractSignatureAuditLogs");

            migrationBuilder.DropTable(
                name: "ContractSignatureSigners");

            migrationBuilder.DropTable(
                name: "ContractSignatureRequests");
        }
    }
}
