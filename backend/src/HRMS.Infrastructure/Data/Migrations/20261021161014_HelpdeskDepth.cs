using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HelpdeskDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueSoonRemindedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TypeId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAtUtc",
                table: "TicketComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TicketComments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AgentCanClose",
                table: "TicketCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentMode",
                table: "TicketCategories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AutoCloseDays",
                table: "TicketCategories",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadEmployeeId",
                table: "TicketCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReopenDays",
                table: "TicketCategories",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.CreateTable(
                name: "HelpdeskSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusLabels = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PriorityLabels = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetHours = table.Column<int>(type: "int", nullable: true),
                    AgentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTypes_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TypeId",
                table: "Tickets",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_LeadEmployeeId",
                table: "TicketCategories",
                column: "LeadEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSettings_TenantId",
                table: "HelpdeskSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_CategoryId_Name",
                table: "TicketTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketCategories_Employees_LeadEmployeeId",
                table: "TicketCategories",
                column: "LeadEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketTypes_TypeId",
                table: "Tickets",
                column: "TypeId",
                principalTable: "TicketTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketCategories_Employees_LeadEmployeeId",
                table: "TicketCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketTypes_TypeId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "HelpdeskSettings");

            migrationBuilder.DropTable(
                name: "TicketTypes");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TypeId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketCategories_LeadEmployeeId",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "DueSoonRemindedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EditedAtUtc",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "AgentCanClose",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "AutoCloseDays",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "LeadEmployeeId",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "ReopenDays",
                table: "TicketCategories");
        }
    }
}
