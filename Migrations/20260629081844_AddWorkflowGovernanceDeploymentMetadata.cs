using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowGovernanceDeploymentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeploymentMode",
                table: "WorkflowDefinitionVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Stable");

            migrationBuilder.AddColumn<string>(
                name: "DeploymentRing",
                table: "WorkflowDefinitionVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RollbackOfVersionId",
                table: "WorkflowDefinitionVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrafficPercentage",
                table: "WorkflowDefinitionVersions",
                type: "int",
                nullable: false,
                defaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeploymentMode",
                table: "WorkflowDefinitionVersions");

            migrationBuilder.DropColumn(
                name: "DeploymentRing",
                table: "WorkflowDefinitionVersions");

            migrationBuilder.DropColumn(
                name: "RollbackOfVersionId",
                table: "WorkflowDefinitionVersions");

            migrationBuilder.DropColumn(
                name: "TrafficPercentage",
                table: "WorkflowDefinitionVersions");
        }
    }
}

