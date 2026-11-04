using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomationLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomationHolder",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationLastHolder",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutomationLastRunAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutomationLeaseUntilUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationStateJson",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomationHolder",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutomationLastHolder",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutomationLastRunAtUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutomationLeaseUntilUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutomationStateJson",
                table: "Tenants");
        }
    }
}
