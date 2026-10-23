using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LetterDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoUse",
                table: "LetterTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Footer",
                table: "LetterTemplates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginSideMm",
                table: "LetterTemplates",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "MarginTopMm",
                table: "LetterTemplates",
                type: "int",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "LetterTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SelfService",
                table: "LetterTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "IssuedLetters",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Footer",
                table: "IssuedLetters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginSideMm",
                table: "IssuedLetters",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "MarginTopMm",
                table: "IssuedLetters",
                type: "int",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<string>(
                name: "RequestReason",
                table: "IssuedLetters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Requested",
                table: "IssuedLetters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "IssuedLetters",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "IssuedLetters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CandidateLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IssuedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MarginTopMm = table.Column<int>(type: "int", nullable: false),
                    MarginSideMm = table.Column<int>(type: "int", nullable: false),
                    Footer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateLetters_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateLetters_ApplicationId",
                table: "CandidateLetters",
                column: "ApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "AutoUse",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "Footer",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "MarginSideMm",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "MarginTopMm",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "SelfService",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "Footer",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "MarginSideMm",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "MarginTopMm",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "RequestReason",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "Requested",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "IssuedLetters");
        }
    }
}
