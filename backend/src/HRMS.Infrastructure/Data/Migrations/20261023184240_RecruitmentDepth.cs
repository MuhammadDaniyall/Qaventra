using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScreenCities",
                table: "JobPosts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreenKeywords",
                table: "JobPosts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScreenMaxExpectedSalary",
                table: "JobPosts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScreenMinExperienceYears",
                table: "JobPosts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Interviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarksJson",
                table: "InterviewFeedback",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMarks",
                table: "InterviewFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalMarks",
                table: "InterviewFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalToken",
                table: "Candidates",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterviewTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterviewQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxMarks = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewQuestions_InterviewTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InterviewTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_PortalToken",
                table: "Candidates",
                column: "PortalToken",
                unique: true,
                filter: "[PortalToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuestions_TemplateId",
                table: "InterviewQuestions",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewQuestions");

            migrationBuilder.DropTable(
                name: "InterviewTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Candidates_PortalToken",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ScreenCities",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ScreenKeywords",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ScreenMaxExpectedSalary",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ScreenMinExperienceYears",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "MarksJson",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "MaxMarks",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "TotalMarks",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "PortalToken",
                table: "Candidates");
        }
    }
}
