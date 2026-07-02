using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveCaseManagementConnectorsAndProcessMining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentWorkflowInstanceId",
                table: "WorkflowInstances",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConnectorDeadLetterMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectorName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRetriedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorDeadLetterMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectorName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowCaseTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    TaskType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubCaseInstanceId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowCaseTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowCaseTasks_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowCaseTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowCaseTasks_WorkflowInstances_SubCaseInstanceId",
                        column: x => x.SubCaseInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowCaseTasks_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowCaseTasks_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromStepNumber = table.Column<int>(type: "int", nullable: true),
                    ToStepNumber = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StationKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    StationName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CorrelationKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionEvents_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionEvents_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_ParentWorkflowInstanceId",
                table: "WorkflowInstances",
                column: "ParentWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorDeadLetterMessages_Status_FailedAt",
                table: "ConnectorDeadLetterMessages",
                columns: new[] { "Status", "FailedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorExecutionLogs_ConnectorName_ExecutedAt",
                table: "ConnectorExecutionLogs",
                columns: new[] { "ConnectorName", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseTasks_AssignedToUserId",
                table: "WorkflowCaseTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseTasks_CreatedByUserId",
                table: "WorkflowCaseTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseTasks_SubCaseInstanceId",
                table: "WorkflowCaseTasks",
                column: "SubCaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseTasks_WorkflowInstanceId_Status_TaskType",
                table: "WorkflowCaseTasks",
                columns: new[] { "WorkflowInstanceId", "Status", "TaskType" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseTasks_WorkflowStepId",
                table: "WorkflowCaseTasks",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionEvents_ActorUserId",
                table: "WorkflowTransitionEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionEvents_StationKey_OccurredAt",
                table: "WorkflowTransitionEvents",
                columns: new[] { "StationKey", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionEvents_WorkflowInstanceId_SequenceNumber",
                table: "WorkflowTransitionEvents",
                columns: new[] { "WorkflowInstanceId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionEvents_WorkflowStepId",
                table: "WorkflowTransitionEvents",
                column: "WorkflowStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_WorkflowInstances_ParentWorkflowInstanceId",
                table: "WorkflowInstances",
                column: "ParentWorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_WorkflowInstances_ParentWorkflowInstanceId",
                table: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "ConnectorDeadLetterMessages");

            migrationBuilder.DropTable(
                name: "ConnectorExecutionLogs");

            migrationBuilder.DropTable(
                name: "WorkflowCaseTasks");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionEvents");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_ParentWorkflowInstanceId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ParentWorkflowInstanceId",
                table: "WorkflowInstances");
        }
    }
}

