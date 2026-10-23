using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Recruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CareersSlug",
                table: "Tenants",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Cnic = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrentTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CurrentCompany = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExperienceYears = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    ExpectedSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NoticeDays = table.Column<int>(type: "int", nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ReferredBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsBlacklisted = table.Column<bool>(type: "bit", nullable: false),
                    BlacklistReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Candidates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JobRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Positions = table.Column<int>(type: "int", nullable: false),
                    ReplacesEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExperienceMinYears = table.Column<int>(type: "int", nullable: true),
                    Qualification = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NeededBy = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequests_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequests_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "Designations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequests_Employees_ReplacesEmployeeId",
                        column: x => x.ReplacesEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequests_Employees_RequestedByEmployeeId",
                        column: x => x.RequestedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ShowSalary = table.Column<bool>(type: "bit", nullable: false),
                    Positions = table.Column<int>(type: "int", nullable: false),
                    ClosesOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecruiterEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HiringManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPosts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobPosts_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "Designations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobPosts_Employees_HiringManagerEmployeeId",
                        column: x => x.HiringManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobPosts_Employees_RecruiterEmployeeId",
                        column: x => x.RecruiterEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobPosts_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    CoverLetter = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OfferedSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OfferedJoiningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OfferStatus = table.Column<int>(type: "int", nullable: true),
                    StageChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobPosts_JobPostId",
                        column: x => x.JobPostId,
                        principalTable: "JobPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsNote = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Where = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InterviewerIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interviews_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<int>(type: "int", nullable: false),
                    Strengths = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Concerns = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewFeedback_Employees_InterviewerEmployeeId",
                        column: x => x.InterviewerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterviewFeedback_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CareersSlug",
                table: "Tenants",
                column: "CareersSlug",
                unique: true,
                filter: "[CareersSlug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_ApplicationId",
                table: "ApplicationEvents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_EmployeeId",
                table: "Candidates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_TenantId_Email",
                table: "Candidates",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewFeedback_InterviewerEmployeeId",
                table: "InterviewFeedback",
                column: "InterviewerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewFeedback_InterviewId_InterviewerEmployeeId",
                table: "InterviewFeedback",
                columns: new[] { "InterviewId", "InterviewerEmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ApplicationId",
                table: "Interviews",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantId_StartsAtUtc",
                table: "Interviews",
                columns: new[] { "TenantId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_CandidateId",
                table: "JobApplications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobPostId_CandidateId",
                table: "JobApplications",
                columns: new[] { "JobPostId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_TenantId_Stage",
                table: "JobApplications",
                columns: new[] { "TenantId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_DepartmentId",
                table: "JobPosts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_DesignationId",
                table: "JobPosts",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_HiringManagerEmployeeId",
                table: "JobPosts",
                column: "HiringManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_JobRequestId",
                table: "JobPosts",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_RecruiterEmployeeId",
                table: "JobPosts",
                column: "RecruiterEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_TenantId_Slug",
                table: "JobPosts",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_TenantId_Status",
                table: "JobPosts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_DepartmentId",
                table: "JobRequests",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_DesignationId",
                table: "JobRequests",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_ReplacesEmployeeId",
                table: "JobRequests",
                column: "ReplacesEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_RequestedByEmployeeId",
                table: "JobRequests",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_TenantId_Status",
                table: "JobRequests",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationEvents");

            migrationBuilder.DropTable(
                name: "InterviewFeedback");

            migrationBuilder.DropTable(
                name: "Interviews");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "JobPosts");

            migrationBuilder.DropTable(
                name: "JobRequests");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CareersSlug",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CareersSlug",
                table: "Tenants");
        }
    }
}
