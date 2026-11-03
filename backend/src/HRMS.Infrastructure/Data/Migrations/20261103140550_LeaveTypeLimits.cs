using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeaveTypeLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxBackdatedDays",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDaysInAdvance",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyLimit",
                table: "LeaveTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxBackdatedDays",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MaxDaysInAdvance",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "LeaveTypes");
        }
    }
}
