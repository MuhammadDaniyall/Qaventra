using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseTypesAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseTypeId",
                table: "ExpenseItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromCity",
                table: "ExpenseItem",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "ExpenseItem",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToCity",
                table: "ExpenseItem",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceAdjusted",
                table: "ExpenseClaims",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AdvanceId",
                table: "ExpenseClaims",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DistanceRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistanceRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Adjusted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Recovered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAdvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseAdvances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Calculation = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PerClaimLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    YearlyLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReceiptRequired = table.Column<bool>(type: "bit", nullable: false),
                    GradeIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseTypeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    YearlyLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTypeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseTypeRates_ExpenseTypes_ExpenseTypeId",
                        column: x => x.ExpenseTypeId,
                        principalTable: "ExpenseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseTypeRates_OrgLookups_GradeId",
                        column: x => x.GradeId,
                        principalTable: "OrgLookups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistanceRoutes_TenantId_FromCity_ToCity",
                table: "DistanceRoutes",
                columns: new[] { "TenantId", "FromCity", "ToCity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAdvances_EmployeeId",
                table: "ExpenseAdvances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypeRates_ExpenseTypeId",
                table: "ExpenseTypeRates",
                column: "ExpenseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypeRates_GradeId",
                table: "ExpenseTypeRates",
                column: "GradeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistanceRoutes");

            migrationBuilder.DropTable(
                name: "ExpenseAdvances");

            migrationBuilder.DropTable(
                name: "ExpenseTypeRates");

            migrationBuilder.DropTable(
                name: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ExpenseTypeId",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "FromCity",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "ToCity",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "AdvanceAdjusted",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "AdvanceId",
                table: "ExpenseClaims");
        }
    }
}
