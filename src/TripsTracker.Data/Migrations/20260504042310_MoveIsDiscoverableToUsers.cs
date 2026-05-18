using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveIsDiscoverableToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresLogin",
                table: "ShareLinks");

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscoverable",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDiscoverable",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLogin",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
