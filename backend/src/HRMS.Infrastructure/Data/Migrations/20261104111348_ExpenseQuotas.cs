using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CarryForward",
                table: "ExpenseTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ForGender",
                table: "ExpenseTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyLimit",
                table: "ExpenseTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProRata",
                table: "ExpenseTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Taxable",
                table: "ExpenseTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarryForward",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ForGender",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ProRata",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "Taxable",
                table: "ExpenseTypes");
        }
    }
}
