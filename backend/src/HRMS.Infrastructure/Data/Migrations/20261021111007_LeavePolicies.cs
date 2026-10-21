using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeavePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Accrual",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "AllowHalfDay",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowShortLeave",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowedOnProbation",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicableGender",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarryForwardMax",
                table: "LeaveTypes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompensatory",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxConsecutiveDays",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinNoticeDays",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "LeaveRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HalfDayPart",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "LeaveRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Adjustment",
                table: "LeaveBalances",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentNote",
                table: "LeaveBalances",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedForward",
                table: "LeaveBalances",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Credited",
                table: "LeaveBalances",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CompOffRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompOffRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompOffRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompOffRequests_EmployeeId_WorkedOn",
                table: "CompOffRequests",
                columns: new[] { "EmployeeId", "WorkedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "Accrual",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "AllowHalfDay",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "AllowShortLeave",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "AllowedOnProbation",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ApplicableGender",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "CarryForwardMax",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "IsCompensatory",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MaxConsecutiveDays",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MinNoticeDays",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "HalfDayPart",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Adjustment",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "AdjustmentNote",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "CarriedForward",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "Credited",
                table: "LeaveBalances");
        }
    }
}
