using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Scheduled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DaysBefore = table.Column<int>(type: "int", nullable: false),
                    ProbationMonths = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    SendHour = table.Column<int>(type: "int", nullable: false, defaultValue: 9),
                    ToEmployee = table.Column<bool>(type: "bit", nullable: false),
                    ToManager = table.Column<bool>(type: "bit", nullable: false),
                    ToHr = table.Column<bool>(type: "bit", nullable: false),
                    ToEveryone = table.Column<bool>(type: "bit", nullable: false),
                    ExtraRecipientIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TitleTemplate = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    SendHour = table.Column<int>(type: "int", nullable: false, defaultValue: 9),
                    RecipientIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExtraEmails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Recipients = table.Column<int>(type: "int", nullable: false),
                    Items = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Manual = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_NextRunAtUtc",
                table: "ScheduledReports",
                column: "NextRunAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledRuns_TenantId_CreatedAtUtc",
                table: "ScheduledRuns",
                columns: new[] { "TenantId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledAlerts");

            migrationBuilder.DropTable(
                name: "ScheduledReports");

            migrationBuilder.DropTable(
                name: "ScheduledRuns");
        }
    }
}
