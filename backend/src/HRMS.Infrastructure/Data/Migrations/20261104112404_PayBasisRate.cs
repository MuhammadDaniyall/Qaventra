using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayBasisRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayBasis",
                table: "EmployeeSalaries",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "EmployeeSalaries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayBasis",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "EmployeeSalaries");
        }
    }
}
