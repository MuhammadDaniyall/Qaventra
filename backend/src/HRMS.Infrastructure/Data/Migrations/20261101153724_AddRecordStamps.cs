using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkExperiences",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkExperiences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "WorkExperiences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "WorkExperiences",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "WorkExperiences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Users",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TravelSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TravelSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TravelSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TravelSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TravelSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TravelRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TravelRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TravelRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TravelRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TravelRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TravelReasons",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TravelReasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TravelReasons",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TravelReasons",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TravelReasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TravelLegs",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TravelLegs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TravelLegs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TravelLegs",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TravelLegs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TravelExpenses",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TravelExpenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TravelExpenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TravelExpenses",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TravelExpenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TrainingSessions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TrainingSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TrainingSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TrainingSessions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TrainingSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TrainingRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TrainingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TrainingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TrainingRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TrainingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TrainingPrograms",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TrainingPrograms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TrainingPrograms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TrainingPrograms",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TrainingPrograms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TrainingEnrollments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TrainingEnrollments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TrainingEnrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TrainingEnrollments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TrainingEnrollments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TrainingCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TrainingCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TrainingCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TrainingCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TrainingCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Timesheets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Timesheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Timesheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Timesheets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Timesheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TimesheetLines",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TimesheetLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TimesheetLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TimesheetLines",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TimesheetLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TicketTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TicketTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TicketTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TicketTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TicketTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Tickets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Tickets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TicketComments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TicketComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TicketComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TicketComments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TicketComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TicketCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TicketCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TicketCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TicketCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TicketCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Tenants",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Tenants",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TenantPayments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TenantPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TenantPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TenantPayments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TenantPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaxYears",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TaxYears",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TaxYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TaxYears",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TaxYears",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaxSlab",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TaxSlab",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TaxSlab",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TaxSlab",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TaxSlab",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaxAdjustments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TaxAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "TaxAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TaxAdjustments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "TaxAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Suggestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Suggestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Suggestions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Suggestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Suggestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SuggestionCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SuggestionCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SuggestionCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SuggestionCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SuggestionCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Spaces",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Spaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Spaces",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Spaces",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Spaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SpaceBookings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SpaceBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SpaceBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SpaceBookings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SpaceBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Shifts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Shifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Shifts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ShiftRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ShiftRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ShiftRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ShiftRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ShiftRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ShiftAssignments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ShiftAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ShiftAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ShiftAssignments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ShiftAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Separations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Separations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Separations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Separations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Separations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ScheduledRuns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ScheduledRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ScheduledRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ScheduledRuns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ScheduledRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ScheduledReports",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ScheduledReports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ScheduledReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ScheduledReports",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ScheduledReports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ScheduledAlerts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ScheduledAlerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ScheduledAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ScheduledAlerts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ScheduledAlerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SalarySplits",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SalarySplits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SalarySplits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SalarySplits",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SalarySplits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SalaryRevisions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SalaryRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SalaryRevisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SalaryRevisions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SalaryRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SalaryHolds",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SalaryHolds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SalaryHolds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SalaryHolds",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SalaryHolds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SalaryComponent",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SalaryComponent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "SalaryComponent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SalaryComponent",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "SalaryComponent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReviewItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReviewItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ReviewItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ReviewItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ReviewItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReviewFeedback",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReviewFeedback",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ReviewFeedback",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ReviewFeedback",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ReviewFeedback",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReviewCycles",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReviewCycles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ReviewCycles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ReviewCycles",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ReviewCycles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReviewCheckIns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReviewCheckIns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ReviewCheckIns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ReviewCheckIns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ReviewCheckIns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReportSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ReportSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ReportSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ReportSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RecurringPayItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "RecurringPayItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "RecurringPayItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "RecurringPayItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "RecurringPayItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Qualifications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Qualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Qualifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Qualifications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Qualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PushSubscriptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PushSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PushSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PushSubscriptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PushSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PushKeys",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PushKeys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PushKeys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PushKeys",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PushKeys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProjectTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ProjectTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ProjectTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ProjectTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Projects",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Projects",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProjectMembers",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ProjectMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ProjectMembers",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProfileChangeRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ProfileChangeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ProfileChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ProfileChangeRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ProfileChangeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PollVotes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PollVotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PollVotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PollVotes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PollVotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Polls",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Polls",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Polls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Polls",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Polls",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PollOptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PollOptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PollOptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PollOptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PollOptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PieceRates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PieceRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PieceRates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PieceRates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PieceRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PieceItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PieceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PieceItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PieceItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PieceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PieceEntries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PieceEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PieceEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PieceEntries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PieceEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PieceCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PieceCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PieceCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PieceCategories",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PieceCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PerformanceTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PerformanceTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PerformanceTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PerformanceTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PerformanceTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PerformanceSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PerformanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PerformanceSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PerformanceSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PerformanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PerformanceReviews",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PerformanceReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PerformanceReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PerformanceReviews",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PerformanceReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PenaltyRules",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PenaltyRules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PenaltyRules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PenaltyRules",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PenaltyRules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PenaltyCharges",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PenaltyCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PenaltyCharges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PenaltyCharges",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PenaltyCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Payslips",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Payslips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Payslips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Payslips",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Payslips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PayslipLine",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PayslipLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PayslipLine",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PayslipLine",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PayslipLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PayrollSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PayrollSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PayrollSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PayrollSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PayrollSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PayrollRuns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PayrollRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PayrollRuns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PayrollGlMappings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PayrollGlMappings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PayrollGlMappings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PayrollGlMappings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PayrollGlMappings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PayRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PayRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "PayRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PayRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "PayRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OrgLookups",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OrgLookups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OrgLookups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OrgLookups",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OrgLookups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OnboardingTemplateTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OnboardingTemplateTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OnboardingTemplateTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OnboardingTemplateTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OnboardingTemplateTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OnboardingTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OnboardingTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OnboardingTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OnboardingTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OnboardingTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OnboardingTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OnboardingTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OnboardingTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OnboardingTasks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OnboardingTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Onboardings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Onboardings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Onboardings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Onboardings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Onboardings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OfficeLocations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OfficeLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OfficeLocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OfficeLocations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OfficeLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OffDayRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "OffDayRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "OffDayRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "OffDayRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "OffDayRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Notifications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Notifications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Loans",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Loans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Loans",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Loans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LoanRepayments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LoanRepayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LoanRepayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LoanRepayments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LoanRepayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LetterTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LetterTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LetterTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LetterTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LetterTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LetterSequences",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LetterSequences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LetterSequences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LetterSequences",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LetterSequences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaveTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LeaveTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LeaveTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LeaveTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LeaveTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaveRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LeaveRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaveReasons",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LeaveReasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LeaveReasons",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LeaveReasons",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LeaveReasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaveBalances",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "LeaveBalances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "LeaveBalances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "LeaveBalances",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "LeaveBalances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Kpis",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Kpis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Kpis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Kpis",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Kpis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "JobRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "JobRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "JobRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "JobRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "JobRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "JobPosts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "JobPosts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "JobPosts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "JobPosts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "JobPosts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "JobChanges",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "JobChanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "JobChanges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "JobChanges",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "JobChanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "JobApplications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "JobApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "JobApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "JobApplications",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "JobApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "IssuedLetters",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "IssuedLetters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "IssuedLetters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "IssuedLetters",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "IssuedLetters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "InterviewTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InterviewTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "InterviewTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "InterviewTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "InterviewTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Interviews",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Interviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Interviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Interviews",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Interviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "InterviewQuestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InterviewQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "InterviewQuestions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "InterviewQuestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "InterviewQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "InterviewFeedback",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InterviewFeedback",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "InterviewFeedback",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "InterviewFeedback",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "InterviewFeedback",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Holidays",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Holidays",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Holidays",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Holidays",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Holidays",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HelpdeskSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "HelpdeskSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "HelpdeskSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "HelpdeskSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "HelpdeskSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "GlAccounts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "GlAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "GlAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "GlAccounts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "GlAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FundWithdrawals",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "FundWithdrawals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "FundWithdrawals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "FundWithdrawals",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "FundWithdrawals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FundEntries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "FundEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "FundEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "FundEntries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "FundEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Floors",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Floors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Floors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Floors",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Floors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseTypeRates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseTypeRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseTypeRates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseTypeRates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseTypeRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseReceipts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseReceipts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseItem",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseItem",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseItem",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseClaims",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseClaims",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseClaims",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseClaims",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExpenseAdvances",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExpenseAdvances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExpenseAdvances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExpenseAdvances",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExpenseAdvances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExitQuestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExitQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExitQuestions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExitQuestions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExitQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExitInterviewTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ExitInterviewTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ExitInterviewTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExitInterviewTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ExitInterviewTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmployeeSalaries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeSalaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "EmployeeSalaries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmployeeSalaries",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "EmployeeSalaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Employees",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Employees",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmergencyContacts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmergencyContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "EmergencyContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmergencyContacts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "EmergencyContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmailTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmailTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "EmailTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmailTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "EmailTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmailLogs",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmailLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "EmailLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmailLogs",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "EmailLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "DistanceRoutes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "DistanceRoutes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "DistanceRoutes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "DistanceRoutes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "DistanceRoutes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "DeskSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "DeskSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "DeskSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "DeskSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "DeskSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Designations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Designations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Designations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Designations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Designations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Dependants",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Dependants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Dependants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Dependants",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Dependants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Departments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Departments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Departments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CustomFieldValues",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "CustomFieldValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "CustomFieldValues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CustomFieldValues",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "CustomFieldValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CustomFields",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "CustomFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "CustomFields",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CustomFields",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "CustomFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CompOffRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "CompOffRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "CompOffRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CompOffRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "CompOffRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ClearanceTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ClearanceTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ClearanceTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ClearanceTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ClearanceTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ClearanceTemplateItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ClearanceTemplateItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ClearanceTemplateItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ClearanceTemplateItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ClearanceTemplateItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ClearanceItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ClearanceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ClearanceItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ClearanceItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ClearanceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CelebrationWishes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "CelebrationWishes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "CelebrationWishes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CelebrationWishes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "CelebrationWishes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Candidates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Candidates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Candidates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Candidates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Candidates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CandidateLetters",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "CandidateLetters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "CandidateLetters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CandidateLetters",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "CandidateLetters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BudgetPositions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "BudgetPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "BudgetPositions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "BudgetPositions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "BudgetPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BudgetPlans",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "BudgetPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "BudgetPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "BudgetPlans",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "BudgetPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BiometricDevices",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "BiometricDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "BiometricDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "BiometricDevices",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "BiometricDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "AuditLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                table: "AuditLogs",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendanceSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendanceSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendanceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendanceRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendanceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceRecords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendanceRecords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendancePeriods",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendancePeriods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendancePeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendancePeriods",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendancePeriods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceExemptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceExemptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendanceExemptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendanceExemptions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendanceExemptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceBreaks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceBreaks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttendanceBreaks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttendanceBreaks",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttendanceBreaks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Attachments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Attachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Attachments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttachmentContents",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttachmentContents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AttachmentContents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AttachmentContents",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AttachmentContents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AssetTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AssetTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AssetTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AssetTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Assets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Assets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Assets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Assets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AssetRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AssetRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AssetRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AssetRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AssetKits",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetKits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AssetKits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AssetKits",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AssetKits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AssetAssignments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AssetAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AssetAssignments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AssetAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApprovalSteps",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApprovalSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApprovalSteps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApprovalSteps",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApprovalSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApprovalRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApprovalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApprovalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApprovalRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApprovalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApprovalFlowSteps",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApprovalFlowSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApprovalFlowSteps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApprovalFlowSteps",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApprovalFlowSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApprovalFlows",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApprovalFlows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApprovalFlows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApprovalFlows",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApprovalFlows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApprovalDelegations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApprovalDelegations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApprovalDelegations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApprovalDelegations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApprovalDelegations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApplicationEvents",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApplicationEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApplicationEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApplicationEvents",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApplicationEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApiTokens",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ApiTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ApiTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ApiTokens",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "ApiTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Announcements",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Announcements",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AnnouncementReads",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AnnouncementReads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AnnouncementReads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AnnouncementReads",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AnnouncementReads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AccessTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AccessTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AccessTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AccessTemplates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedById",
                table: "AccessTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002501"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002502"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002503"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002504"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002505"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002506"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002601"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002602"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002603"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002604"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002605"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002606"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002607"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxSlab",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0002-4000-8000-000000002608"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxYears",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0001-4000-8000-000000002526"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            migrationBuilder.UpdateData(
                table: "TaxYears",
                keyColumn: "Id",
                keyValue: new Guid("6a1f1c2e-0001-4000-8000-000000002627"),
                columns: new[] { "CreatedBy", "CreatedById", "ModifiedAtUtc", "ModifiedBy", "ModifiedById" },
                values: new object[] { "System", null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null });

            BackfillStamps(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TravelSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TravelSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TravelSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TravelSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TravelSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TravelReasons");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TravelReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TravelReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TravelReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TravelReasons");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TravelLegs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TravelLegs");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TravelLegs");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TravelLegs");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TravelLegs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TravelExpenses");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TravelExpenses");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TravelExpenses");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TravelExpenses");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TravelExpenses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TrainingRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TrainingRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TrainingRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TrainingRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TrainingRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TrainingEnrollments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TrainingEnrollments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TrainingEnrollments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TrainingEnrollments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TrainingEnrollments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TrainingCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TrainingCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TrainingCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TrainingCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TrainingCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Timesheets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Timesheets");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Timesheets");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Timesheets");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Timesheets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaxSlab");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TaxSlab");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TaxSlab");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TaxSlab");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TaxSlab");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "TaxAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SuggestionCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SuggestionCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SuggestionCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SuggestionCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SuggestionCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Separations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ScheduledRuns");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ScheduledRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ScheduledRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ScheduledRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ScheduledRuns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ScheduledAlerts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ScheduledAlerts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ScheduledAlerts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ScheduledAlerts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ScheduledAlerts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SalarySplits");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SalarySplits");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SalarySplits");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SalarySplits");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SalarySplits");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SalaryRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SalaryRevisions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SalaryRevisions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SalaryRevisions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SalaryRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SalaryHolds");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SalaryHolds");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SalaryHolds");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SalaryHolds");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SalaryHolds");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SalaryComponent");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SalaryComponent");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "SalaryComponent");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SalaryComponent");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "SalaryComponent");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReviewItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReviewItems");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ReviewItems");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ReviewItems");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ReviewItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReviewFeedback");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ReviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ReviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ReviewFeedback");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReviewCheckIns");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReviewCheckIns");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ReviewCheckIns");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ReviewCheckIns");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ReviewCheckIns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RecurringPayItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "RecurringPayItems");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "RecurringPayItems");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "RecurringPayItems");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "RecurringPayItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PushKeys");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PushKeys");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PushKeys");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PushKeys");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PushKeys");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProfileChangeRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ProfileChangeRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ProfileChangeRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ProfileChangeRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ProfileChangeRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PollVotes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PollVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PollVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PollVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PollVotes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PieceRates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PieceRates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PieceRates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PieceRates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PieceRates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PieceItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PieceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PieceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PieceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PieceItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PieceEntries");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PieceEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PieceEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PieceEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PieceEntries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PieceCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PieceCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PieceCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PieceCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PieceCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PerformanceSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PerformanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PerformanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PerformanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PerformanceSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PerformanceReviews");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PenaltyCharges");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PenaltyCharges");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PenaltyCharges");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PenaltyCharges");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PenaltyCharges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PayslipLine");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PayslipLine");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PayslipLine");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PayslipLine");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PayslipLine");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PayrollGlMappings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PayrollGlMappings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PayrollGlMappings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PayrollGlMappings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PayrollGlMappings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PayRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "PayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PayRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OrgLookups");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OrgLookups");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OrgLookups");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OrgLookups");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OrgLookups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OnboardingTemplateTasks");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OnboardingTemplateTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OnboardingTemplateTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OnboardingTemplateTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OnboardingTemplateTasks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OnboardingTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OnboardingTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OnboardingTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OnboardingTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OnboardingTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OfficeLocations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OfficeLocations");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OfficeLocations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OfficeLocations");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OfficeLocations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OffDayRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "OffDayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "OffDayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "OffDayRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "OffDayRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LetterSequences");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LetterSequences");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LetterSequences");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LetterSequences");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LetterSequences");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaveReasons");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LeaveReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LeaveReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LeaveReasons");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LeaveReasons");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "IssuedLetters");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "InterviewFeedback");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HelpdeskSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "HelpdeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "HelpdeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "HelpdeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "HelpdeskSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FundWithdrawals");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "FundWithdrawals");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "FundWithdrawals");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "FundWithdrawals");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "FundWithdrawals");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FundEntries");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "FundEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "FundEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "FundEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "FundEntries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Floors");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Floors");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Floors");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Floors");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Floors");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseTypeRates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseTypeRates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseTypeRates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseTypeRates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseTypeRates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseReceipts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseReceipts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseReceipts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExpenseAdvances");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExpenseAdvances");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExpenseAdvances");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExpenseAdvances");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExpenseAdvances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExitQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExitQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExitQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExitQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExitQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ExitInterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DistanceRoutes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "DistanceRoutes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "DistanceRoutes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "DistanceRoutes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "DistanceRoutes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DeskSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "DeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "DeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "DeskSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "DeskSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Dependants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Dependants");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Dependants");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Dependants");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Dependants");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "CompOffRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ClearanceTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ClearanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ClearanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ClearanceTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ClearanceTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ClearanceTemplateItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ClearanceTemplateItems");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ClearanceTemplateItems");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ClearanceTemplateItems");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ClearanceTemplateItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ClearanceItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CelebrationWishes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "CelebrationWishes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "CelebrationWishes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CelebrationWishes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "CelebrationWishes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "BudgetPositions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EmployeeName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendancePeriods");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendancePeriods");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendancePeriods");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendancePeriods");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendancePeriods");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceExemptions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceExemptions");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendanceExemptions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendanceExemptions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendanceExemptions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceBreaks");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceBreaks");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttendanceBreaks");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttendanceBreaks");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttendanceBreaks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttachmentContents");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttachmentContents");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AttachmentContents");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AttachmentContents");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AttachmentContents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AssetRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AssetRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AssetRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AssetRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AssetKits");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetKits");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AssetKits");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AssetKits");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AssetKits");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApprovalFlowSteps");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApprovalFlowSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApprovalFlowSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApprovalFlowSteps");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApprovalFlowSteps");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApprovalFlows");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApprovalFlows");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApprovalFlows");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApprovalFlows");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApprovalFlows");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AnnouncementReads");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AnnouncementReads");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AnnouncementReads");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AnnouncementReads");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AnnouncementReads");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AccessTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AccessTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AccessTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AccessTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "AccessTemplates");
        }
            /// <summary>Every BaseEntity at this migration: the audit trail's EntityType (the class name) and its table.</summary>
        private static readonly (string Entity, string Table)[] StampedTables =
        [
            ("AccessTemplate", "AccessTemplates"),
            ("Announcement", "Announcements"),
            ("AnnouncementRead", "AnnouncementReads"),
            ("ApiToken", "ApiTokens"),
            ("ApplicationEvent", "ApplicationEvents"),
            ("ApprovalDelegation", "ApprovalDelegations"),
            ("ApprovalFlow", "ApprovalFlows"),
            ("ApprovalFlowStep", "ApprovalFlowSteps"),
            ("ApprovalRequest", "ApprovalRequests"),
            ("ApprovalStep", "ApprovalSteps"),
            ("Asset", "Assets"),
            ("AssetAssignment", "AssetAssignments"),
            ("AssetKit", "AssetKits"),
            ("AssetRequest", "AssetRequests"),
            ("AssetType", "AssetTypes"),
            ("Attachment", "Attachments"),
            ("AttachmentContent", "AttachmentContents"),
            ("AttendanceBreak", "AttendanceBreaks"),
            ("AttendanceExemption", "AttendanceExemptions"),
            ("AttendancePeriod", "AttendancePeriods"),
            ("AttendanceRecord", "AttendanceRecords"),
            ("AttendanceRequest", "AttendanceRequests"),
            ("AttendanceSettings", "AttendanceSettings"),
            ("BiometricDevice", "BiometricDevices"),
            ("BudgetPlan", "BudgetPlans"),
            ("BudgetPosition", "BudgetPositions"),
            ("Candidate", "Candidates"),
            ("CandidateLetter", "CandidateLetters"),
            ("CelebrationWish", "CelebrationWishes"),
            ("ClearanceItem", "ClearanceItems"),
            ("ClearanceTemplate", "ClearanceTemplates"),
            ("ClearanceTemplateItem", "ClearanceTemplateItems"),
            ("CompOffRequest", "CompOffRequests"),
            ("CustomField", "CustomFields"),
            ("CustomFieldValue", "CustomFieldValues"),
            ("Department", "Departments"),
            ("Dependant", "Dependants"),
            ("Designation", "Designations"),
            ("DeskSettings", "DeskSettings"),
            ("DistanceRoute", "DistanceRoutes"),
            ("EmailLog", "EmailLogs"),
            ("EmailTemplate", "EmailTemplates"),
            ("EmergencyContact", "EmergencyContacts"),
            ("Employee", "Employees"),
            ("EmployeeSalary", "EmployeeSalaries"),
            ("ExitInterviewTemplate", "ExitInterviewTemplates"),
            ("ExitQuestion", "ExitQuestions"),
            ("ExpenseAdvance", "ExpenseAdvances"),
            ("ExpenseClaim", "ExpenseClaims"),
            ("ExpenseItem", "ExpenseItem"),
            ("ExpenseReceipt", "ExpenseReceipts"),
            ("ExpenseType", "ExpenseTypes"),
            ("ExpenseTypeRate", "ExpenseTypeRates"),
            ("Floor", "Floors"),
            ("FundEntry", "FundEntries"),
            ("FundWithdrawal", "FundWithdrawals"),
            ("GlAccount", "GlAccounts"),
            ("HelpdeskSettings", "HelpdeskSettings"),
            ("Holiday", "Holidays"),
            ("Interview", "Interviews"),
            ("InterviewFeedback", "InterviewFeedback"),
            ("InterviewQuestion", "InterviewQuestions"),
            ("InterviewTemplate", "InterviewTemplates"),
            ("IssuedLetter", "IssuedLetters"),
            ("JobApplication", "JobApplications"),
            ("JobChange", "JobChanges"),
            ("JobPost", "JobPosts"),
            ("JobRequest", "JobRequests"),
            ("Kpi", "Kpis"),
            ("LeaveBalance", "LeaveBalances"),
            ("LeaveReason", "LeaveReasons"),
            ("LeaveRequest", "LeaveRequests"),
            ("LeaveType", "LeaveTypes"),
            ("LetterSequence", "LetterSequences"),
            ("LetterTemplate", "LetterTemplates"),
            ("Loan", "Loans"),
            ("LoanRepayment", "LoanRepayments"),
            ("Notification", "Notifications"),
            ("OffDayRequest", "OffDayRequests"),
            ("OfficeLocation", "OfficeLocations"),
            ("Onboarding", "Onboardings"),
            ("OnboardingTask", "OnboardingTasks"),
            ("OnboardingTemplate", "OnboardingTemplates"),
            ("OnboardingTemplateTask", "OnboardingTemplateTasks"),
            ("OrgLookup", "OrgLookups"),
            ("PayRequest", "PayRequests"),
            ("PayrollGlMapping", "PayrollGlMappings"),
            ("PayrollRun", "PayrollRuns"),
            ("PayrollSettings", "PayrollSettings"),
            ("Payslip", "Payslips"),
            ("PayslipLine", "PayslipLine"),
            ("PenaltyCharge", "PenaltyCharges"),
            ("PenaltyRule", "PenaltyRules"),
            ("PerformanceReview", "PerformanceReviews"),
            ("PerformanceSettings", "PerformanceSettings"),
            ("PerformanceTemplate", "PerformanceTemplates"),
            ("PieceCategory", "PieceCategories"),
            ("PieceEntry", "PieceEntries"),
            ("PieceItem", "PieceItems"),
            ("PieceRate", "PieceRates"),
            ("Poll", "Polls"),
            ("PollOption", "PollOptions"),
            ("PollVote", "PollVotes"),
            ("ProfileChangeRequest", "ProfileChangeRequests"),
            ("Project", "Projects"),
            ("ProjectMember", "ProjectMembers"),
            ("ProjectTask", "ProjectTasks"),
            ("PushKeys", "PushKeys"),
            ("PushSubscription", "PushSubscriptions"),
            ("Qualification", "Qualifications"),
            ("RecurringPayItem", "RecurringPayItems"),
            ("ReportSettings", "ReportSettings"),
            ("ReviewCheckIn", "ReviewCheckIns"),
            ("ReviewCycle", "ReviewCycles"),
            ("ReviewFeedback", "ReviewFeedback"),
            ("ReviewItem", "ReviewItems"),
            ("SalaryComponent", "SalaryComponent"),
            ("SalaryHold", "SalaryHolds"),
            ("SalaryRevision", "SalaryRevisions"),
            ("SalarySplit", "SalarySplits"),
            ("ScheduledAlert", "ScheduledAlerts"),
            ("ScheduledReport", "ScheduledReports"),
            ("ScheduledRun", "ScheduledRuns"),
            ("Separation", "Separations"),
            ("Shift", "Shifts"),
            ("ShiftAssignment", "ShiftAssignments"),
            ("ShiftRequest", "ShiftRequests"),
            ("Space", "Spaces"),
            ("SpaceBooking", "SpaceBookings"),
            ("Suggestion", "Suggestions"),
            ("SuggestionCategory", "SuggestionCategories"),
            ("TaxAdjustment", "TaxAdjustments"),
            ("TaxSlab", "TaxSlab"),
            ("TaxYear", "TaxYears"),
            ("Tenant", "Tenants"),
            ("TenantPayment", "TenantPayments"),
            ("Ticket", "Tickets"),
            ("TicketCategory", "TicketCategories"),
            ("TicketComment", "TicketComments"),
            ("TicketType", "TicketTypes"),
            ("Timesheet", "Timesheets"),
            ("TimesheetLine", "TimesheetLines"),
            ("TrainingCategory", "TrainingCategories"),
            ("TrainingEnrollment", "TrainingEnrollments"),
            ("TrainingProgram", "TrainingPrograms"),
            ("TrainingRequest", "TrainingRequests"),
            ("TrainingSession", "TrainingSessions"),
            ("TravelExpense", "TravelExpenses"),
            ("TravelLeg", "TravelLegs"),
            ("TravelReason", "TravelReasons"),
            ("TravelRequest", "TravelRequests"),
            ("TravelSettings", "TravelSettings"),
            ("User", "Users"),
            ("WorkExperience", "WorkExperiences"),
        ];

        /// <summary>The display snapshot "First Last (CODE)", or "Name (Role)" for a login without an employee record.</summary>
        private const string ActorDisplay = @"LEFT(CASE
    WHEN e.Id IS NOT NULL THEN LTRIM(RTRIM(e.FirstName + N' ' + e.LastName)) + N' (' + e.EmployeeCode + N')'
    WHEN a.UserId IS NULL AND a.UserEmail IS NULL THEN CASE WHEN a.EntityType = N'Suggestion' THEN N'Anonymous' ELSE N'System' END
    ELSE COALESCE(NULLIF(u.FullName, N''), u.Email, a.UserEmail) + COALESCE(N' (' + CASE COALESCE(u.Role, a.Role)
        WHEN 0 THEN N'Owner' WHEN 1 THEN N'Company Super Admin' WHEN 2 THEN N'HR manager' WHEN 3 THEN N'Manager' WHEN 4 THEN N'Employee' END + N')', N'')
END, 250)";

        /// <summary>
        /// Existing rows: Modified starts at Created, and who added and last changed each row comes from the Data audit
        /// trail where it has the row. Rows the trail never saw keep null and screens show "—". The trail is read once into
        /// a keyed work table (a real table, so idempotent scripts work too), then each table is updated in one statement.
        /// </summary>
        private static void BackfillStamps(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
CREATE TABLE [__RecordStamps] (
    EntityType nvarchar(100) NOT NULL, EntityId nvarchar(100) NOT NULL,
    CreatedById uniqueidentifier NULL, CreatedBy nvarchar(250) NULL,
    ModifiedAtUtc datetime2 NOT NULL, ModifiedById uniqueidentifier NULL, ModifiedBy nvarchar(250) NULL,
    CONSTRAINT [PK___RecordStamps] PRIMARY KEY (EntityType, EntityId));

WITH latest AS (
    SELECT a.EntityType, a.EntityId, a.TimestampUtc, a.UserId, {ActorDisplay} AS Display,
        ROW_NUMBER() OVER (PARTITION BY a.EntityType, a.EntityId ORDER BY a.Id DESC) AS N
    FROM AuditLogs a
    LEFT JOIN Users u ON u.Id = a.UserId
    LEFT JOIN Employees e ON e.Id = u.EmployeeId
    WHERE a.Category = 3 AND a.Action IN (N'Created', N'Updated') AND a.EntityType IS NOT NULL AND a.EntityId IS NOT NULL)
INSERT INTO [__RecordStamps] (EntityType, EntityId, ModifiedAtUtc, ModifiedById, ModifiedBy)
SELECT EntityType, EntityId, TimestampUtc, UserId, Display FROM latest WHERE N = 1;

WITH created AS (
    SELECT a.EntityType, a.EntityId, a.UserId, {ActorDisplay} AS Display,
        ROW_NUMBER() OVER (PARTITION BY a.EntityType, a.EntityId ORDER BY a.Id) AS N
    FROM AuditLogs a
    LEFT JOIN Users u ON u.Id = a.UserId
    LEFT JOIN Employees e ON e.Id = u.EmployeeId
    WHERE a.Category = 3 AND a.Action = N'Created' AND a.EntityType IS NOT NULL AND a.EntityId IS NOT NULL)
UPDATE s SET CreatedById = c.UserId, CreatedBy = c.Display
FROM [__RecordStamps] s
JOIN created c ON c.EntityType = s.EntityType AND c.EntityId = s.EntityId AND c.N = 1;");

            foreach (var (entity, table) in StampedTables)
            {
                migrationBuilder.Sql($@"
UPDATE t SET
    CreatedById = s.CreatedById, CreatedBy = COALESCE(s.CreatedBy, t.CreatedBy),
    ModifiedAtUtc = COALESCE(s.ModifiedAtUtc, t.CreatedAtUtc),
    ModifiedById = s.ModifiedById, ModifiedBy = COALESCE(s.ModifiedBy, t.ModifiedBy)
FROM [{table}] t
LEFT JOIN [__RecordStamps] s ON s.EntityType = N'{entity}' AND s.EntityId = LOWER(CONVERT(nvarchar(36), t.Id))
WHERE t.ModifiedAtUtc IS NULL;");
            }

            // Reviewers must never learn who sent an anonymous suggestion, including from its sender's own later edits.
            // Poll votes were never in the trail, so they have no names to hide.
            migrationBuilder.Sql(@"
UPDATE Suggestions SET CreatedById = NULL, CreatedBy = N'Anonymous' WHERE IsAnonymous = 1;
UPDATE s SET ModifiedById = NULL, ModifiedBy = N'Anonymous'
FROM Suggestions s
LEFT JOIN Users u ON u.Id = s.ModifiedById
WHERE s.IsAnonymous = 1 AND (s.ModifiedById IS NULL OR u.EmployeeId = s.EmployeeId);

DROP TABLE [__RecordStamps];");
        }
    }
}
