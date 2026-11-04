using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PenaltyRuleScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForGender",
                table: "PenaltyRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForStatus",
                table: "PenaltyRules",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForGender",
                table: "PenaltyRules");

            migrationBuilder.DropColumn(
                name: "ForStatus",
                table: "PenaltyRules");
        }
    }
}
