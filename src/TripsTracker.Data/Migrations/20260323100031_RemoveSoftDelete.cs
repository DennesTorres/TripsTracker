using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hard-delete rows that were previously soft-deleted
            migrationBuilder.Sql("DELETE FROM Places WHERE IsDeleted = 1;");
            migrationBuilder.Sql("DELETE FROM Countries WHERE IsDeleted = 1;");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Countries");

            // Recreate VisitedStates VIEW without IsDeleted filter (column no longer exists)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert VisitedStates VIEW to version with IsDeleted filter
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
                    WHERE IsDeleted = 0 AND StateAbbr IS NOT NULL
                    GROUP BY CountryId, StateAbbr
                ) AS p;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Places",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Countries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
