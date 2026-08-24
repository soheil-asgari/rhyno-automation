using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.OfficeDb
{
    /// <inheritdoc />
    public partial class FixContractAttachmentForeignKeyCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments",
                column: "CurrentVersionId",
                principalTable: "ContractAttachmentVersions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAttachments_ContractAttachmentVersions_CurrentVersionId",
                table: "ContractAttachments",
                column: "CurrentVersionId",
                principalTable: "ContractAttachmentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
