using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeWorkflowPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_AssignedToUserId",
                table: "WorkflowSteps");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUserId",
                table: "WorkflowSteps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<int>(
                name: "AssignedDepartmentId",
                table: "WorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedRoleId",
                table: "WorkflowSteps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "WorkflowSteps",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DelegatedFromUserId",
                table: "WorkflowSteps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                table: "WorkflowSteps",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "WorkflowSteps",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnedFromStepNumber",
                table: "WorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaState",
                table: "WorkflowSteps",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepKey",
                table: "WorkflowSteps",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepName",
                table: "WorkflowSteps",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApproverUserId",
                table: "WorkflowRoutes",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<bool>(
                name: "AllowDelegation",
                table: "WorkflowRoutes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowReturn",
                table: "WorkflowRoutes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApproverDepartmentId",
                table: "WorkflowRoutes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverRoleId",
                table: "WorkflowRoutes",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "WorkflowRoutes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EscalationHours",
                table: "WorkflowRoutes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SlaHours",
                table: "WorkflowRoutes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StepName",
                table: "WorkflowRoutes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "WorkflowInstances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentAssigneeDepartmentId",
                table: "WorkflowInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentAssigneeRoleId",
                table: "WorkflowInstances",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentAssigneeUserId",
                table: "WorkflowInstances",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStatus",
                table: "WorkflowInstances",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAt",
                table: "WorkflowInstances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "WorkflowInstances",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttachmentCount",
                table: "WorkflowDecisions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DecisionType",
                table: "WorkflowDecisions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignatureText",
                table: "WorkflowDecisions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowActionLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowActionLogs_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowActionLogs_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    WorkflowDecisionId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowAttachments_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowAttachments_WorkflowDecisions_WorkflowDecisionId",
                        column: x => x.WorkflowDecisionId,
                        principalTable: "WorkflowDecisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowAttachments_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowAttachments_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowComments_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowComments_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowComments_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowEscalationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    EscalatedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EscalatedToRoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PreviousSlaState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NewSlaState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowEscalationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowEscalationEvents_AspNetRoles_EscalatedToRoleId",
                        column: x => x.EscalatedToRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowEscalationEvents_AspNetUsers_EscalatedToUserId",
                        column: x => x.EscalatedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowEscalationEvents_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowEscalationEvents_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_AssignedDepartmentId_Status_DueAt",
                table: "WorkflowSteps",
                columns: new[] { "AssignedDepartmentId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_AssignedRoleId_Status_DueAt",
                table: "WorkflowSteps",
                columns: new[] { "AssignedRoleId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_AssignedToUserId_Status_DueAt",
                table: "WorkflowSteps",
                columns: new[] { "AssignedToUserId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_ReadAt_CreatedAt",
                table: "WorkflowSteps",
                columns: new[] { "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowInstanceId_Status_DueAt",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowInstanceId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_DelegatedFromUserId",
                table: "WorkflowSteps",
                column: "DelegatedFromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRoutes_ApproverDepartmentId",
                table: "WorkflowRoutes",
                column: "ApproverDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRoutes_ApproverRoleId",
                table: "WorkflowRoutes",
                column: "ApproverRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeDepartmentId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeRoleId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeUserId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeUserId_Status_DueAt",
                table: "WorkflowInstances",
                columns: new[] { "CurrentAssigneeUserId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status_SlaState_DueAt",
                table: "WorkflowInstances",
                columns: new[] { "Status", "SlaState", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionLogs_ActorUserId",
                table: "WorkflowActionLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionLogs_WorkflowInstanceId_OccurredAt",
                table: "WorkflowActionLogs",
                columns: new[] { "WorkflowInstanceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionLogs_WorkflowStepId",
                table: "WorkflowActionLogs",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachments_UploadedByUserId",
                table: "WorkflowAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachments_WorkflowDecisionId",
                table: "WorkflowAttachments",
                column: "WorkflowDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachments_WorkflowInstanceId_UploadedAt",
                table: "WorkflowAttachments",
                columns: new[] { "WorkflowInstanceId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachments_WorkflowStepId",
                table: "WorkflowAttachments",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComments_AuthorUserId",
                table: "WorkflowComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComments_WorkflowInstanceId_CreatedAt",
                table: "WorkflowComments",
                columns: new[] { "WorkflowInstanceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComments_WorkflowStepId",
                table: "WorkflowComments",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEscalationEvents_EscalatedToRoleId",
                table: "WorkflowEscalationEvents",
                column: "EscalatedToRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEscalationEvents_EscalatedToUserId",
                table: "WorkflowEscalationEvents",
                column: "EscalatedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEscalationEvents_WorkflowInstanceId",
                table: "WorkflowEscalationEvents",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEscalationEvents_WorkflowStepId",
                table: "WorkflowEscalationEvents",
                column: "WorkflowStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_AspNetRoles_CurrentAssigneeRoleId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeRoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_AspNetUsers_CurrentAssigneeUserId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_Departments_CurrentAssigneeDepartmentId",
                table: "WorkflowInstances",
                column: "CurrentAssigneeDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowRoutes_AspNetRoles_ApproverRoleId",
                table: "WorkflowRoutes",
                column: "ApproverRoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowRoutes_Departments_ApproverDepartmentId",
                table: "WorkflowRoutes",
                column: "ApproverDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_AspNetRoles_AssignedRoleId",
                table: "WorkflowSteps",
                column: "AssignedRoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_AspNetUsers_DelegatedFromUserId",
                table: "WorkflowSteps",
                column: "DelegatedFromUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_Departments_AssignedDepartmentId",
                table: "WorkflowSteps",
                column: "AssignedDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_AspNetRoles_CurrentAssigneeRoleId",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_AspNetUsers_CurrentAssigneeUserId",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Departments_CurrentAssigneeDepartmentId",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowRoutes_AspNetRoles_ApproverRoleId",
                table: "WorkflowRoutes");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowRoutes_Departments_ApproverDepartmentId",
                table: "WorkflowRoutes");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_AspNetRoles_AssignedRoleId",
                table: "WorkflowSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_AspNetUsers_DelegatedFromUserId",
                table: "WorkflowSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_Departments_AssignedDepartmentId",
                table: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "WorkflowActionLogs");

            migrationBuilder.DropTable(
                name: "WorkflowAttachments");

            migrationBuilder.DropTable(
                name: "WorkflowComments");

            migrationBuilder.DropTable(
                name: "WorkflowEscalationEvents");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_AssignedDepartmentId_Status_DueAt",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_AssignedRoleId_Status_DueAt",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_AssignedToUserId_Status_DueAt",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_ReadAt_CreatedAt",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_WorkflowInstanceId_Status_DueAt",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_DelegatedFromUserId",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRoutes_ApproverDepartmentId",
                table: "WorkflowRoutes");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRoutes_ApproverRoleId",
                table: "WorkflowRoutes");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeDepartmentId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeRoleId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeUserId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_CurrentAssigneeUserId_Status_DueAt",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status_SlaState_DueAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "AssignedDepartmentId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AssignedRoleId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DelegatedFromUserId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ReturnedFromStepNumber",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "SlaState",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepKey",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepName",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AllowDelegation",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "AllowReturn",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "ApproverDepartmentId",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "ApproverRoleId",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "EscalationHours",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "SlaHours",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "StepName",
                table: "WorkflowRoutes");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CurrentAssigneeDepartmentId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CurrentAssigneeRoleId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CurrentAssigneeUserId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CurrentStatus",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "LastActionAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "AttachmentCount",
                table: "WorkflowDecisions");

            migrationBuilder.DropColumn(
                name: "DecisionType",
                table: "WorkflowDecisions");

            migrationBuilder.DropColumn(
                name: "SignatureText",
                table: "WorkflowDecisions");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUserId",
                table: "WorkflowSteps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApproverUserId",
                table: "WorkflowRoutes",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_AssignedToUserId",
                table: "WorkflowSteps",
                column: "AssignedToUserId");
        }
    }
}

