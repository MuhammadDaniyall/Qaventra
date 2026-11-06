using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GulfPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GratuityAmount",
                table: "Separations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SocialInsuranceEmployee",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SocialInsuranceEmployer",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PayrollSettings",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PKR");

            migrationBuilder.AddColumn<decimal>(
                name: "GosiAnnuityEmployeePercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 9m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiAnnuityEmployerPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 9m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiHazardEmployerPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiNewEntrantMaxPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 11m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiNewEntrantStepPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiSanedEmployeePercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0.75m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiSanedEmployerPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0.75m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiWageCeiling",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 45000m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiWageFloor",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1500m);

            migrationBuilder.AddColumn<int>(
                name: "GulfGratuityWage",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayrollCountry",
                table: "PayrollSettings",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "PK");

            migrationBuilder.AddColumn<decimal>(
                name: "PensionEmployeePercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 11m);

            migrationBuilder.AddColumn<decimal>(
                name: "PensionEmployerPercent",
                table: "PayrollSettings",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 12.5m);

            migrationBuilder.AddColumn<bool>(
                name: "PensionOnGross",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PensionWageCeiling",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 70000m);

            migrationBuilder.AddColumn<decimal>(
                name: "PensionWageFloor",
                table: "PayrollSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 3000m);

            migrationBuilder.AddColumn<string>(
                name: "WpsAgentId",
                table: "PayrollSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WpsBankRoutingCode",
                table: "PayrollSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WpsEmployerIban",
                table: "PayrollSettings",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WpsEmployerId",
                table: "PayrollSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PayrollRuns",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PKR");

            migrationBuilder.AddColumn<string>(
                name: "PayrollCountry",
                table: "PayrollRuns",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "PK");

            migrationBuilder.AddColumn<string>(
                name: "GccNationality",
                table: "Employees",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GosiNewEntrant",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Employees",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IqamaNo",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabourCardNo",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GratuityAmount",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SocialInsuranceEmployee",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SocialInsuranceEmployer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiAnnuityEmployeePercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiAnnuityEmployerPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiHazardEmployerPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiNewEntrantMaxPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiNewEntrantStepPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiSanedEmployeePercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiSanedEmployerPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiWageCeiling",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GosiWageFloor",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "GulfGratuityWage",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PayrollCountry",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PensionEmployeePercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PensionEmployerPercent",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PensionOnGross",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PensionWageCeiling",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PensionWageFloor",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "WpsAgentId",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "WpsBankRoutingCode",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "WpsEmployerIban",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "WpsEmployerId",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PayrollCountry",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "GccNationality",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GosiNewEntrant",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IqamaNo",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LabourCardNo",
                table: "Employees");
        }
    }
}
