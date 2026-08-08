using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.TaxPersistence
{
    /// <inheritdoc />
    public partial class AddMowadiyanAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MowadiyanSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MemorySecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrivateKeyPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrivateKeyPassword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EconomicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BaseUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsSandbox = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MowadiyanSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MowadiyanSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusText = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedEnvelope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FiscalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "int", nullable: false),
                    IsSandbox = table.Column<bool>(type: "bit", nullable: false),
                    NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MowadiyanSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MowadiyanSubmissions_InvoiceRecords_InvoiceRecordId",
                        column: x => x.InvoiceRecordId,
                        principalTable: "InvoiceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceReconciliationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TaxReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PortalTotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PortalVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PortalVatRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    PortalSellerEconomicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PortalBuyerEconomicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DiscrepancyCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceReconciliationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceReconciliationResults_InvoiceRecords_InvoiceRecordId",
                        column: x => x.InvoiceRecordId,
                        principalTable: "InvoiceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceReconciliationResults_MowadiyanSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "MowadiyanSubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LocalValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PortalValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Suggestion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationDiscrepancies_InvoiceReconciliationResults_ReconciliationResultId",
                        column: x => x.ReconciliationResultId,
                        principalTable: "InvoiceReconciliationResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceReconciliationResults_SubmissionId",
                table: "InvoiceReconciliationResults",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResult_InvoiceRecordId",
                table: "InvoiceReconciliationResults",
                column: "InvoiceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResult_Status",
                table: "InvoiceReconciliationResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MowadiyanSubmission_InvoiceRecordId",
                table: "MowadiyanSubmissions",
                column: "InvoiceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MowadiyanSubmission_Status",
                table: "MowadiyanSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MowadiyanSubmission_TaxReferenceId",
                table: "MowadiyanSubmissions",
                column: "TaxReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancy_ReconciliationResultId",
                table: "ReconciliationDiscrepancies",
                column: "ReconciliationResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MowadiyanSettings");

            migrationBuilder.DropTable(
                name: "ReconciliationDiscrepancies");

            migrationBuilder.DropTable(
                name: "InvoiceReconciliationResults");

            migrationBuilder.DropTable(
                name: "MowadiyanSubmissions");
        }
    }
}
