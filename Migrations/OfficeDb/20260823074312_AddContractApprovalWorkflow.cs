using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class AddContractApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractApprovalDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractApprovalDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractApprovalDelegations_AspNetUsers_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractApprovalDelegations_AspNetUsers_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FinalComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReturnMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentStepNumber = table.Column<int>(type: "int", nullable: false),
                    ExpiryHours = table.Column<int>(type: "int", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractApprovalRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractApprovalRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractApprovalRequestId = table.Column<int>(type: "int", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    StepTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssigneeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedRoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MinThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReturnToStepNumber = table.Column<int>(type: "int", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DelegatedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StepExpiryHours = table.Column<int>(type: "int", nullable: true),
                    StepExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractApprovalSteps_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractApprovalSteps_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractApprovalSteps_AspNetUsers_DelegatedToUserId",
                        column: x => x.DelegatedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractApprovalSteps_ContractApprovalRequests_ContractApprovalRequestId",
                        column: x => x.ContractApprovalRequestId,
                        principalTable: "ContractApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachments_UploadedByUserId",
                table: "ContractAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalDelegations_FromUserId",
                table: "ContractApprovalDelegations",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalDelegations_IsActive",
                table: "ContractApprovalDelegations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalDelegations_ToUserId",
                table: "ContractApprovalDelegations",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalRequests_ContractId",
                table: "ContractApprovalRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalRequests_RequestedByUserId",
                table: "ContractApprovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalRequests_Status",
                table: "ContractApprovalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalSteps_AssignedUserId",
                table: "ContractApprovalSteps",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalSteps_ContractApprovalRequestId",
                table: "ContractApprovalSteps",
                column: "ContractApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalSteps_DecidedByUserId",
                table: "ContractApprovalSteps",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalSteps_Decision",
                table: "ContractApprovalSteps",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovalSteps_DelegatedToUserId",
                table: "ContractApprovalSteps",
                column: "DelegatedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAttachments_AspNetUsers_UploadedByUserId",
                table: "ContractAttachments",
                column: "UploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAttachments_AspNetUsers_UploadedByUserId",
                table: "ContractAttachments");

            migrationBuilder.DropTable(
                name: "ContractApprovalDelegations");

            migrationBuilder.DropTable(
                name: "ContractApprovalSteps");

            migrationBuilder.DropTable(
                name: "ContractApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ContractAttachments_UploadedByUserId",
                table: "ContractAttachments");
        }
    }
}
