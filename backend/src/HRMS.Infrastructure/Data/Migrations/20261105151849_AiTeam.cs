using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RobotsStopped",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RobotsStoppedBy",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiRobots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Autonomy = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunHour = table.Column<int>(type: "int", nullable: false),
                    RunsPerMonth = table.Column<int>(type: "int", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRobots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiRobotProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRobotProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRobotProposals_AiRobots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "AiRobots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AiRobotRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Manual = table.Column<bool>(type: "bit", nullable: false),
                    StartedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Proposals = table.Column<int>(type: "int", nullable: false),
                    UsedAi = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRobotRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRobotRuns_AiRobots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "AiRobots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiRobotProposals_RobotId",
                table: "AiRobotProposals",
                column: "RobotId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRobotProposals_TenantId_RobotId_SubjectKey",
                table: "AiRobotProposals",
                columns: new[] { "TenantId", "RobotId", "SubjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRobotProposals_TenantId_Status",
                table: "AiRobotProposals",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRobotRuns_RobotId",
                table: "AiRobotRuns",
                column: "RobotId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRobotRuns_TenantId_RobotId_StartedAtUtc",
                table: "AiRobotRuns",
                columns: new[] { "TenantId", "RobotId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRobots_TenantId_Kind",
                table: "AiRobots",
                columns: new[] { "TenantId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRobotProposals");

            migrationBuilder.DropTable(
                name: "AiRobotRuns");

            migrationBuilder.DropTable(
                name: "AiRobots");

            migrationBuilder.DropColumn(
                name: "RobotsStopped",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RobotsStoppedBy",
                table: "Tenants");
        }
    }
}
