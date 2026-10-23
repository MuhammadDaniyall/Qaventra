using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Travel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TravelReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TravelSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinNoticeDays = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    PerDiemDomestic = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PerDiemInternational = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SettleWithinDays = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    DeskIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TravelRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TripType = table.Column<int>(type: "int", nullable: false),
                    IsInternational = table.Column<bool>(type: "bit", nullable: false),
                    DepartOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UrgentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NeedsHotel = table.Column<bool>(type: "bit", nullable: false),
                    HotelCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: true),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: true),
                    NeedsPickDrop = table.Column<bool>(type: "bit", nullable: false),
                    PickDropNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdvanceRequested = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HotelName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    HotelRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HotelCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BookingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BookedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BookedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdvancePaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdvancePaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AdvancePaidVia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Settlement = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PerDiemDays = table.Column<int>(type: "int", nullable: true),
                    PerDiemRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SettlementNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SettlementSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettlementDecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TravelRequests_PayRequests_PayRequestId",
                        column: x => x.PayRequestId,
                        principalTable: "PayRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TravelRequests_TravelReasons_ReasonId",
                        column: x => x.ReasonId,
                        principalTable: "TravelReasons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TravelExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TravelRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelExpenses_TravelRequests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalTable: "TravelRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TravelLegs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TravelRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    To = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Preference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BookingRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BookingCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelLegs_TravelRequests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalTable: "TravelRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TravelExpenses_TravelRequestId",
                table: "TravelExpenses",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelLegs_TravelRequestId",
                table: "TravelLegs",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelReasons_TenantId_Name",
                table: "TravelReasons",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_EmployeeId",
                table: "TravelRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_PayRequestId",
                table: "TravelRequests",
                column: "PayRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_ReasonId",
                table: "TravelRequests",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_TenantId_Status",
                table: "TravelRequests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelSettings_TenantId",
                table: "TravelSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TravelExpenses");

            migrationBuilder.DropTable(
                name: "TravelLegs");

            migrationBuilder.DropTable(
                name: "TravelSettings");

            migrationBuilder.DropTable(
                name: "TravelRequests");

            migrationBuilder.DropTable(
                name: "TravelReasons");
        }
    }
}
