using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class AddAutoRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoRenewDaysBeforeExpiry",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AutoRenewTermMonths",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoRenewalCancelledAt",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoRenewalCancelledByUserId",
                table: "Contracts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoRenew",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAutoRenewedAt",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentRequirementId",
                table: "ContractAttachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutoRenewalLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    RenewedContractId = table.Column<int>(type: "int", nullable: true),
                    RenewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewalTermMonths = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRenewalLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoRenewalLogs_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutoRenewalLogs_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalculatedPenalties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    CalculatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CalculationBasis = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalculatedPenalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalculatedPenalties_ContractPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "ContractPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalculatedPenalties_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractGuarantees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    AmendmentId = table.Column<int>(type: "int", nullable: true),
                    GuaranteeType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GuaranteeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuingBank = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RetentionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ReleaseCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReleasedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractGuarantees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractGuarantees_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractGuarantees_ContractAmendments_AmendmentId",
                        column: x => x.AmendmentId,
                        principalTable: "ContractAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContractGuarantees_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractTypeFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Text"),
                    Options = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Placeholder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidationPattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValidationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTypeFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DocumentCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPenaltyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    PenaltyType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PenaltyPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CapType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CapValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPenaltyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPenaltyRules_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractTypeFieldId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractFieldValues_ContractTypeFields_ContractTypeFieldId",
                        column: x => x.ContractTypeFieldId,
                        principalTable: "ContractTypeFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractFieldValues_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AutoRenewalCancelledByUserId",
                table: "Contracts",
                column: "AutoRenewalCancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachments_DocumentRequirementId",
                table: "ContractAttachments",
                column: "DocumentRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoRenewalLogs_CancelledByUserId",
                table: "AutoRenewalLogs",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoRenewalLogs_ContractId",
                table: "AutoRenewalLogs",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoRenewalLogs_RenewedContractId",
                table: "AutoRenewalLogs",
                column: "RenewedContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoRenewalLogs_Status",
                table: "AutoRenewalLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CalculatedPenalties_ContractId",
                table: "CalculatedPenalties",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_CalculatedPenalties_PaymentId",
                table: "CalculatedPenalties",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CalculatedPenalties_Status",
                table: "CalculatedPenalties",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractFieldValues_ContractId_ContractTypeFieldId",
                table: "ContractFieldValues",
                columns: new[] { "ContractId", "ContractTypeFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractFieldValues_ContractTypeFieldId",
                table: "ContractFieldValues",
                column: "ContractTypeFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_AmendmentId",
                table: "ContractGuarantees",
                column: "AmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_ContractId",
                table: "ContractGuarantees",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_CreatedByUserId",
                table: "ContractGuarantees",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_ExpiryDate",
                table: "ContractGuarantees",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_GuaranteeNumber",
                table: "ContractGuarantees",
                column: "GuaranteeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ContractGuarantees_Status",
                table: "ContractGuarantees",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTypeFields_ContractType_FieldKey",
                table: "ContractTypeFields",
                columns: new[] { "ContractType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_ContractType",
                table: "DocumentRequirements",
                column: "ContractType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_DocumentCategory",
                table: "DocumentRequirements",
                column: "DocumentCategory");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPenaltyRules_ContractId",
                table: "PaymentPenaltyRules",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPenaltyRules_IsActive",
                table: "PaymentPenaltyRules",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAttachments_DocumentRequirements_DocumentRequirementId",
                table: "ContractAttachments",
                column: "DocumentRequirementId",
                principalTable: "DocumentRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_AutoRenewalCancelledByUserId",
                table: "Contracts",
                column: "AutoRenewalCancelledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAttachments_DocumentRequirements_DocumentRequirementId",
                table: "ContractAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_AutoRenewalCancelledByUserId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "AutoRenewalLogs");

            migrationBuilder.DropTable(
                name: "CalculatedPenalties");

            migrationBuilder.DropTable(
                name: "ContractFieldValues");

            migrationBuilder.DropTable(
                name: "ContractGuarantees");

            migrationBuilder.DropTable(
                name: "DocumentRequirements");

            migrationBuilder.DropTable(
                name: "PaymentPenaltyRules");

            migrationBuilder.DropTable(
                name: "ContractTypeFields");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_AutoRenewalCancelledByUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_ContractAttachments_DocumentRequirementId",
                table: "ContractAttachments");

            migrationBuilder.DropColumn(
                name: "AutoRenewDaysBeforeExpiry",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AutoRenewTermMonths",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AutoRenewalCancelledAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AutoRenewalCancelledByUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsAutoRenew",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LastAutoRenewedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentRequirementId",
                table: "ContractAttachments");
        }
    }
}
