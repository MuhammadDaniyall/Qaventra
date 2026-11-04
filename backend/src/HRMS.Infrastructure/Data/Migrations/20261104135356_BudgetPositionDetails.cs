using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPositionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "BudgetPositions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "BudgetPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GradeId",
                table: "BudgetPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobDescription",
                table: "BudgetPositions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportsToId",
                table: "BudgetPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "BudgetPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "BudgetPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "GradeId",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "JobDescription",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "ReportsToId",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "BudgetPositions");
        }
    }
}
