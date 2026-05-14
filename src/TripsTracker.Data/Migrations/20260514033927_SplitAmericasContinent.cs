using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitAmericasContinent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Split 'Americas' into three separate continents for finer-grained scoring.
            // North America: Canada (CA), United States (US).
            // South America: all mainland South American countries.
            // Central America: Mexico, Central American isthmus, and all Caribbean islands.
            migrationBuilder.Sql("""
                UPDATE Countries
                SET Region = CASE
                    WHEN IsoAlpha2 IN ('CA','US')
                        THEN 'North America'
                    WHEN IsoAlpha2 IN ('AR','BO','BR','CL','CO','EC','GY','PY','PE','SR','UY','VE')
                        THEN 'South America'
                    ELSE 'Central America'
                END
                WHERE Region = 'Americas';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Countries
                SET Region = 'Americas'
                WHERE Region IN ('North America', 'Central America', 'South America');
                """);
        }
    }
}
