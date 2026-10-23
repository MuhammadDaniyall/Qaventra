using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayrollFundsTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PfEmployee",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PfEmployer",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SocialSecurityEmployer",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GratuityDaysPerYear",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.AddColumn<bool>(
                name: "GratuityEnabled",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GratuityMinYears",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<bool>(
                name: "GratuityOnGross",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HolidayOvertimeMultiplier",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<decimal>(
                name: "OffDayOvertimeMultiplier",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeMinMinutes",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeRoundMinutes",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PfEmployeePercent",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 8.33m);

            migrationBuilder.AddColumn<decimal>(
                name: "PfEmployerPercent",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 8.33m);

            migrationBuilder.AddColumn<bool>(
                name: "PfOnGross",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PfVestingYears",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ProvidentFundEnabled",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SocialSecurityPercent",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SocialSecurityWageCeiling",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "ProvidentFundMember",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FundEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fund = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fund = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PayWithSalary = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundWithdrawals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalarySplits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalarySplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalarySplits_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalarySplits_OrgLookups_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "OrgLookups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaxAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxYearCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundEntries_EmployeeId",
                table: "FundEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FundEntries_TenantId_EmployeeId_Fund",
                table: "FundEntries",
                columns: new[] { "TenantId", "EmployeeId", "Fund" });

            migrationBuilder.CreateIndex(
                name: "IX_FundWithdrawals_EmployeeId",
                table: "FundWithdrawals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalarySplits_CostCenterId",
                table: "SalarySplits",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_SalarySplits_EmployeeId",
                table: "SalarySplits",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxAdjustments_EmployeeId",
                table: "TaxAdjustments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxAdjustments_TenantId_EmployeeId_TaxYearCode",
                table: "TaxAdjustments",
                columns: new[] { "TenantId", "EmployeeId", "TaxYearCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundEntries");

            migrationBuilder.DropTable(
                name: "FundWithdrawals");

            migrationBuilder.DropTable(
                name: "SalarySplits");

            migrationBuilder.DropTable(
                name: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "PfEmployee",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "PfEmployer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SocialSecurityEmployer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "GratuityDaysPerYear",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GratuityEnabled",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GratuityMinYears",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GratuityOnGross",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "HolidayOvertimeMultiplier",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "OffDayOvertimeMultiplier",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "OvertimeMinMinutes",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "OvertimeRoundMinutes",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PfEmployeePercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PfEmployerPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PfOnGross",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PfVestingYears",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ProvidentFundEnabled",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "SocialSecurityPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "SocialSecurityWageCeiling",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ProvidentFundMember",
                table: "Employees");
        }
    }
}
