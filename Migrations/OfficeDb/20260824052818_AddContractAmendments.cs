using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class AddContractAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    AmendmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmendmentNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AmountChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EndDateChangeDays = table.Column<int>(type: "int", nullable: true),
                    NewEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalRequestId = table.Column<int>(type: "int", nullable: true),
                    VersionSnapshotId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AppliedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_AspNetUsers_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_ContractApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ContractApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_AppliedByUserId",
                table: "ContractAmendments",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ApprovalRequestId",
                table: "ContractAmendments",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractId",
                table: "ContractAmendments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractId_AmendmentNumber",
                table: "ContractAmendments",
                columns: new[] { "ContractId", "AmendmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_CreatedByUserId",
                table: "ContractAmendments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_Status",
                table: "ContractAmendments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractAmendments");
        }
    }
}
