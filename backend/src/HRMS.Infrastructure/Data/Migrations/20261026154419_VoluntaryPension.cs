using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VoluntaryPension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VpsEmployee",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VpsEmployer",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VpsTaxCredit",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VpsCreditCapPercent",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "VpsEnabled",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VpsAccountNo",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VpsEmployeePercent",
                table: "Employees",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VpsEmployerPercent",
                table: "Employees",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "VpsMember",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VpsProvider",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VpsEmployee",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "VpsEmployer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "VpsTaxCredit",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "VpsCreditCapPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "VpsEnabled",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "VpsAccountNo",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "VpsEmployeePercent",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "VpsEmployerPercent",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "VpsMember",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "VpsProvider",
                table: "Employees");
        }
    }
}
