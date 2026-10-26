using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayFrequencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TenantId_Year_Month",
                table: "PayrollRuns");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BiWeeklyAnchor",
                table: "PayrollSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayWeekStartsOn",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "PayrollRuns",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "PayrollRuns",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "PayrollRuns",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "PayFrequency",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Every run so far was monthly: its period is its calendar month.
            migrationBuilder.Sql("UPDATE PayrollRuns SET PeriodStart = DATEFROMPARTS([Year], [Month], 1), PeriodEnd = EOMONTH(DATEFROMPARTS([Year], [Month], 1))");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_Frequency_PeriodStart",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Frequency", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_Year_Month",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TenantId_Frequency_PeriodStart",
                table: "PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TenantId_Year_Month",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "BiWeeklyAnchor",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PayWeekStartsOn",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PayFrequency",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_Year_Month",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Year", "Month" },
                unique: true);
        }
    }
}
