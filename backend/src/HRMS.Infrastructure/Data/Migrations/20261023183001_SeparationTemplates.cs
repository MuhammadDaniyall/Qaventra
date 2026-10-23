using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExitAnswersJson",
                table: "Separations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitInterviewNotes",
                table: "Separations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitInterviewScheduledUtc",
                table: "Separations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitInterviewerIds",
                table: "Separations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExitTemplateId",
                table: "Separations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementApprovalNote",
                table: "Separations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementApprovedAtUtc",
                table: "Separations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementPaidAtUtc",
                table: "Separations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementPaidVia",
                table: "Separations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementSubmittedAtUtc",
                table: "Separations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "ClearanceItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleEmployeeId",
                table: "ClearanceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClearanceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearanceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExitInterviewTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitInterviewTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClearanceTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ResponsibleEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearanceTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClearanceTemplateItems_ClearanceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ClearanceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExitQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExitQuestions_ExitInterviewTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ExitInterviewTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceTemplateItems_TemplateId",
                table: "ClearanceTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ExitQuestions_TemplateId",
                table: "ExitQuestions",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClearanceTemplateItems");

            migrationBuilder.DropTable(
                name: "ExitQuestions");

            migrationBuilder.DropTable(
                name: "ClearanceTemplates");

            migrationBuilder.DropTable(
                name: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ExitAnswersJson",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ExitInterviewNotes",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ExitInterviewScheduledUtc",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ExitInterviewerIds",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ExitTemplateId",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SettlementApprovalNote",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SettlementApprovedAtUtc",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SettlementPaidAtUtc",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SettlementPaidVia",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "SettlementSubmittedAtUtc",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "ResponsibleEmployeeId",
                table: "ClearanceItems");
        }
    }
}
