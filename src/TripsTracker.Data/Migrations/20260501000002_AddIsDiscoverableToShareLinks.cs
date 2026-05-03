using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDiscoverableToShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDiscoverable",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDiscoverable",
                table: "ShareLinks");
        }
    }
}
