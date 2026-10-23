using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Performance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingLabelsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BellCurveJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaiseSlabsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfReview = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    GoalsDue = table.Column<DateOnly>(type: "date", nullable: true),
                    SelfReviewDue = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagerReviewDue = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfReview = table.Column<bool>(type: "bit", nullable: false),
                    LaunchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewCycles_PerformanceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PerformanceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    SelfComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SelfScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ManagerScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ManagerComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Improvements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DevelopmentPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Potential = table.Column<int>(type: "int", nullable: true),
                    FinalRating = table.Column<int>(type: "int", nullable: true),
                    CalibrationNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EmployeeAgrees = table.Column<bool>(type: "bit", nullable: true),
                    EmployeeSignOffComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    GoalsSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SelfSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedOffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_ReviewerEmployeeId",
                        column: x => x.ReviewerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_ReviewCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Target = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SelfScore = table.Column<int>(type: "int", nullable: true),
                    SelfComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ManagerScore = table.Column<int>(type: "int", nullable: true),
                    ManagerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewItems_PerformanceReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_CycleId_EmployeeId",
                table: "PerformanceReviews",
                columns: new[] { "CycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_EmployeeId",
                table: "PerformanceReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewerEmployeeId",
                table: "PerformanceReviews",
                column: "ReviewerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceSettings_TenantId",
                table: "PerformanceSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTemplates_TenantId_Name",
                table: "PerformanceTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_TemplateId",
                table: "ReviewCycles",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_TenantId_Status",
                table: "ReviewCycles",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewItems_ReviewId",
                table: "ReviewItems",
                column: "ReviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceSettings");

            migrationBuilder.DropTable(
                name: "ReviewItems");

            migrationBuilder.DropTable(
                name: "PerformanceReviews");

            migrationBuilder.DropTable(
                name: "ReviewCycles");

            migrationBuilder.DropTable(
                name: "PerformanceTemplates");
        }
    }
}
