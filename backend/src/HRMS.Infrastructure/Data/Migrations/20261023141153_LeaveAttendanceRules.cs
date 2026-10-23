using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeaveAttendanceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DocumentAfterDays",
                table: "LeaveTypes",
                type: "decimal(6,1)",
                precision: 6,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sandwich",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReasonId",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceExemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Flag = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceExemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceExemptions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExemptionsPerMonth = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    ExemptionWithinDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxDays = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: true),
                    RequiresDocument = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveReasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveReasons_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ReasonId",
                table: "LeaveRequests",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExemptions_EmployeeId_Date",
                table: "AttendanceExemptions",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExemptions_TenantId_Status",
                table: "AttendanceExemptions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSettings_TenantId",
                table: "AttendanceSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveReasons_LeaveTypeId_Name",
                table: "LeaveReasons",
                columns: new[] { "LeaveTypeId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_LeaveReasons_ReasonId",
                table: "LeaveRequests",
                column: "ReasonId",
                principalTable: "LeaveReasons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_LeaveReasons_ReasonId",
                table: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "AttendanceExemptions");

            migrationBuilder.DropTable(
                name: "AttendanceSettings");

            migrationBuilder.DropTable(
                name: "LeaveReasons");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_ReasonId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DocumentAfterDays",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "Sandwich",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ReasonId",
                table: "LeaveRequests");
        }
    }
}
