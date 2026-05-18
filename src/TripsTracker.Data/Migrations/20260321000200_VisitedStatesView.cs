using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisitedStatesView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the manually-managed VisitedStates table
            migrationBuilder.DropTable(name: "VisitedStates");

            // Create a view that derives visited states from Places
            migrationBuilder.Sql("""
                CREATE VIEW VisitedStates AS
                SELECT
                    CAST(ROW_NUMBER() OVER (ORDER BY p.CountryId, p.StateAbbr) AS int) AS Id,
                    p.CountryId,
                    p.StateAbbr
                FROM (
                    SELECT DISTINCT CountryId, StateAbbr
                    FROM Places
                    WHERE IsDeleted = 0 AND StateAbbr IS NOT NULL
                ) AS p;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");

            migrationBuilder.CreateTable(
                name: "VisitedStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StateAbbr = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitedStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitedStates_CountryId_StateAbbr",
                table: "VisitedStates",
                columns: new[] { "CountryId", "StateAbbr" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
