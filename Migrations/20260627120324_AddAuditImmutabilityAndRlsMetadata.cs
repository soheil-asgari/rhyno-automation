using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditImmutabilityAndRlsMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefinitionVersionId",
                table: "WorkflowInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeSet",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserContext",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefinitionVersionId = table.Column<int>(type: "int", nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    AssignmentMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "User"),
                    SlaHours = table.Column<int>(type: "int", nullable: false, defaultValue: 24),
                    EscalationHours = table.Column<int>(type: "int", nullable: false, defaultValue: 48)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepDefinitions_WorkflowDefinitionVersions_DefinitionVersionId",
                        column: x => x.DefinitionVersionId,
                        principalTable: "WorkflowDefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepDefinitionId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextStepKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AssigneeRoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssigneeUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssigneeDepartmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRules_AspNetRoles_AssigneeRoleId",
                        column: x => x.AssigneeRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowRules_AspNetUsers_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowRules_Departments_AssigneeDepartmentId",
                        column: x => x.AssigneeDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowRules_WorkflowStepDefinitions_StepDefinitionId",
                        column: x => x.StepDefinitionId,
                        principalTable: "WorkflowStepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DefinitionVersionId",
                table: "WorkflowInstances",
                column: "DefinitionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TableName_EntityId_DateTime",
                table: "AuditLogs",
                columns: new[] { "TableName", "EntityId", "DateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_DocumentType_IsActive_EffectiveFrom",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "DocumentType", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_DocumentType_Version",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "DocumentType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRules_AssigneeDepartmentId",
                table: "WorkflowRules",
                column: "AssigneeDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRules_AssigneeRoleId",
                table: "WorkflowRules",
                column: "AssigneeRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRules_AssigneeUserId",
                table: "WorkflowRules",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRules_StepDefinitionId_FieldName_Operator",
                table: "WorkflowRules",
                columns: new[] { "StepDefinitionId", "FieldName", "Operator" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepDefinitions_DefinitionVersionId_StepKey",
                table: "WorkflowStepDefinitions",
                columns: new[] { "DefinitionVersionId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepDefinitions_DefinitionVersionId_StepOrder",
                table: "WorkflowStepDefinitions",
                columns: new[] { "DefinitionVersionId", "StepOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitionVersions_DefinitionVersionId",
                table: "WorkflowInstances",
                column: "DefinitionVersionId",
                principalTable: "WorkflowDefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitionVersions_DefinitionVersionId",
                table: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "WorkflowRules");

            migrationBuilder.DropTable(
                name: "WorkflowStepDefinitions");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionVersions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_DefinitionVersionId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TableName_EntityId_DateTime",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DefinitionVersionId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ChangeSet",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserContext",
                table: "AuditLogs");
        }
    }
}

