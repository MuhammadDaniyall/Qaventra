using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalFlowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalFlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalFlowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalFlowSteps_ApprovalFlows_ApprovalFlowId",
                        column: x => x.ApprovalFlowId,
                        principalTable: "ApprovalFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalFlowSteps_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ApproverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlows_TenantId_RequestType",
                table: "ApprovalFlows",
                columns: new[] { "TenantId", "RequestType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlowSteps_ApprovalFlowId",
                table: "ApprovalFlowSteps",
                column: "ApprovalFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlowSteps_EmployeeId",
                table: "ApprovalFlowSteps",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_EmployeeId",
                table: "ApprovalRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestType_SubjectId",
                table: "ApprovalRequests",
                columns: new[] { "RequestType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TenantId_Status",
                table: "ApprovalRequests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalRequestId",
                table: "ApprovalSteps",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApproverEmployeeId_Status",
                table: "ApprovalSteps",
                columns: new[] { "ApproverEmployeeId", "Status" });

            // Requests still pending when chains arrive get the old rule as a one-step chain (line manager, or HR when
            // there is none), so they show in the approvals inbox like new ones.
            // Enum values: RequestType Leave=1 Expense=2; status Pending=1; Kind LineManager=1 Hr=3; step Current=2.
            migrationBuilder.Sql("""
                INSERT INTO ApprovalRequests (Id, TenantId, CreatedAtUtc, RequestType, SubjectId, EmployeeId, Summary, Status, CurrentStep)
                SELECT NEWID(), r.TenantId, r.CreatedAtUtc, 1, r.Id, r.EmployeeId,
                       LEFT(t.Name + N' · ' + FORMAT(r.Days, '0.#') + N' day(s) · ' + FORMAT(r.FromDate, 'd MMM'), 300), 1, 1
                FROM LeaveRequests r JOIN LeaveTypes t ON t.Id = r.LeaveTypeId
                WHERE r.Status = 1;

                INSERT INTO ApprovalRequests (Id, TenantId, CreatedAtUtc, RequestType, SubjectId, EmployeeId, Summary, Status, CurrentStep)
                SELECT NEWID(), c.TenantId, c.CreatedAtUtc, 2, c.Id, c.EmployeeId,
                       LEFT(c.Title + N' · Rs ' + FORMAT(c.Total, 'N0'), 300), 1, 1
                FROM ExpenseClaims c
                WHERE c.Status = 1;

                INSERT INTO ApprovalSteps (Id, CreatedAtUtc, ApprovalRequestId, [Order], Kind, ApproverEmployeeId, Status)
                SELECT NEWID(), a.CreatedAtUtc, a.Id, 1, CASE WHEN e.ManagerId IS NULL THEN 3 ELSE 1 END, e.ManagerId, 2
                FROM ApprovalRequests a JOIN Employees e ON e.Id = a.EmployeeId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalFlowSteps");

            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "ApprovalFlows");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");
        }
    }
}
