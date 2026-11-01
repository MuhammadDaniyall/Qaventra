using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedTaxYear2026_27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A 2026-27 added by hand on the Tax years page would clash with the unique code; payroll refers to it by code only.
            migrationBuilder.Sql("DELETE FROM TaxSlab WHERE TaxYearId IN (SELECT Id FROM TaxYears WHERE Code = N'2026-27'); DELETE FROM TaxYears WHERE Code = N'2026-27';");
            migrationBuilder.InsertData(
                table: "TaxYears",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "EndDate", "StartDate", "SurchargeRate", "SurchargeThreshold" },
                values: new object[] { new Guid("6a1f1c2e-0001-4000-8000-000000002627"), "2026-27", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 6, 30), new DateOnly(2026, 7, 1), 0m, 10000000m });

            migrationBuilder.InsertData(
                table: "TaxSlab",
                columns: new[] { "Id", "CreatedAtUtc", "FixedTax", "FromAmount", "Rate", "TaxYearId", "ToAmount" },
                values: new object[,]
                {
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002601"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, 0m, 0m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 600000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002602"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, 600000m, 0.01m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 1200000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002603"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6000m, 1200000m, 0.11m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 2200000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002604"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 116000m, 2200000m, 0.20m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 3200000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002605"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 316000m, 3200000m, 0.25m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 4100000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002606"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 541000m, 4100000m, 0.29m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 5600000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002607"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 976000m, 5600000m, 0.32m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), 7000000m },
                    { new Guid("6a1f1c2e-0002-4000-8000-000000002608"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1424000m, 7000000m, 0.35m, new Guid("6a1f1c2e-0001-4000-8000-000000002627"), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002601"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002602"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002603"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002604"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002605"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002606"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002607"));

            migrationBuilder.DeleteData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002608"));

            migrationBuilder.DeleteData(
                table: "TaxYears",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0001-4000-8000-000000002627"));
        }
    }
}
