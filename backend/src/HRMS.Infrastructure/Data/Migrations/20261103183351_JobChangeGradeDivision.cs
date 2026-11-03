using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobChangeGradeDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromCostCenter",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromDivision",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromGrade",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToCostCenter",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToDivision",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToGrade",
                table: "JobChanges",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromCostCenter",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "FromDivision",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "FromGrade",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ToCostCenter",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ToDivision",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ToGrade",
                table: "JobChanges");
        }
    }
}
