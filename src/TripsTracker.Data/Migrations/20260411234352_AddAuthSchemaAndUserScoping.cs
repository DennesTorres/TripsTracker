using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSchemaAndUserScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Places",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCountries",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    IsHome = table.Column<bool>(type: "bit", nullable: false),
                    IsVisited = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCountries", x => new { x.UserId, x.CountryId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Places_UserId",
                table: "Places",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_UserId",
                table: "UserCountries",
                column: "UserId");

            // Update VisitedStates VIEW to include UserId — states are now per-user
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");
            migrationBuilder.Sql("""
                CREATE VIEW VisitedStates AS
                SELECT
                    CAST(ROW_NUMBER() OVER (ORDER BY p.UserId, p.CountryId, p.StateAbbr) AS int) AS Id,
                    p.UserId,
                    p.CountryId,
                    p.StateAbbr,
                    p.StateName
                FROM (
                    SELECT UserId, CountryId, StateAbbr, MAX(StateName) AS StateName
                    FROM Places
                    WHERE StateAbbr IS NOT NULL
                    GROUP BY UserId, CountryId, StateAbbr
                ) AS p;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCountries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Places_UserId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Places");

            // Restore VisitedStates VIEW to the pre-auth version (without UserId)
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");
            migrationBuilder.Sql("""
                CREATE VIEW VisitedStates AS
                SELECT
                    CAST(ROW_NUMBER() OVER (ORDER BY p.CountryId, p.StateAbbr) AS int) AS Id,
                    p.CountryId,
                    p.StateAbbr,
                    p.StateName
                FROM (
                    SELECT CountryId, StateAbbr, MAX(StateName) AS StateName
                    FROM Places
                    WHERE StateAbbr IS NOT NULL
                    GROUP BY CountryId, StateAbbr
                ) AS p;
                """);
        }
    }
}
