using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Training : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CompleteWithinDays = table.Column<int>(type: "int", nullable: true),
                    RepeatEveryMonths = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingPrograms_TrainingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TrainingCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TrainingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreferredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingRequests_TrainingPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "TrainingPrograms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrainerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalTrainer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_Employees_TrainerEmployeeId",
                        column: x => x.TrainerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrainingSessions_TrainingPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "TrainingPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NominatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FeedbackRating = table.Column<int>(type: "int", nullable: true),
                    TrainerRating = table.Column<int>(type: "int", nullable: true),
                    WouldRecommend = table.Column<bool>(type: "bit", nullable: true),
                    FeedbackComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FeedbackAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingEnrollments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingEnrollments_TrainingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCategories_TenantId_Name",
                table: "TrainingCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEnrollments_EmployeeId",
                table: "TrainingEnrollments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEnrollments_SessionId_EmployeeId",
                table: "TrainingEnrollments",
                columns: new[] { "SessionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_CategoryId",
                table: "TrainingPrograms",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_TenantId_Title",
                table: "TrainingPrograms",
                columns: new[] { "TenantId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRequests_EmployeeId",
                table: "TrainingRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRequests_ProgramId",
                table: "TrainingRequests",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRequests_TenantId_Status",
                table: "TrainingRequests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_ProgramId",
                table: "TrainingSessions",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TenantId_StartsAtUtc",
                table: "TrainingSessions",
                columns: new[] { "TenantId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TrainerEmployeeId",
                table: "TrainingSessions",
                column: "TrainerEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingEnrollments");

            migrationBuilder.DropTable(
                name: "TrainingRequests");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "TrainingPrograms");

            migrationBuilder.DropTable(
                name: "TrainingCategories");
        }
    }
}
