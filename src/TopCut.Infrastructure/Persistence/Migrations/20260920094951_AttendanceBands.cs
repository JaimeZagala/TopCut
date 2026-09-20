using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopCut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttendanceBands",
                table: "Rulesets",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceBands",
                table: "Rulesets");
        }
    }
}
