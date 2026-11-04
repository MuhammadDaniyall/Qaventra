using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceFinalReviewGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FinalReview",
                table: "ReviewCycles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FinalReviewComment",
                table: "PerformanceReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalReviewedAtUtc",
                table: "PerformanceReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalReviewerEmployeeId",
                table: "PerformanceReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GraceMarks",
                table: "PerformanceReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalReview",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "FinalReviewComment",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "FinalReviewedAtUtc",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "FinalReviewerEmployeeId",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "GraceMarks",
                table: "PerformanceReviews");
        }
    }
}
