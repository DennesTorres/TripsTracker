using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPlacesJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create UserPlaces junction table
            migrationBuilder.CreateTable(
                name: "UserPlaces",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlaceId = table.Column<int>(type: "int", nullable: false),
                    IsHome = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlaces", x => new { x.UserId, x.PlaceId });
                    table.ForeignKey(
                        name: "FK_UserPlaces_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPlaces_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlaces_PlaceId",
                table: "UserPlaces",
                column: "PlaceId");

            // Step 2: Data migration — deduplicate Places into global rows + populate UserPlaces
            // For each (City, CountryId) group, the lowest Id becomes the canonical global Place.
            // Each user gets one UserPlaces row pointing to the canonical Place, carrying IsHome.
            migrationBuilder.Sql(@"
WITH Canonical AS (
    SELECT MIN(Id) AS CanonicalId, City, CountryId
    FROM Places
    GROUP BY City, CountryId
),
UserMapping AS (
    SELECT p.UserId,
           c.CanonicalId AS PlaceId,
           MAX(CAST(p.IsHome AS int)) AS IsHome
    FROM Places p
    JOIN Canonical c ON c.City = p.City AND c.CountryId = p.CountryId
    GROUP BY p.UserId, c.CanonicalId
)
INSERT INTO UserPlaces (UserId, PlaceId, IsHome)
SELECT UserId, PlaceId, CAST(IsHome AS bit)
FROM UserMapping;
");

            // Step 3: Remove non-canonical Place rows (those not referenced by UserPlaces)
            migrationBuilder.Sql(@"
DELETE FROM Places
WHERE Id NOT IN (SELECT PlaceId FROM UserPlaces);
");

            // Step 4: Drop the VisitedStates VIEW (references Places.UserId — must recreate after column drop)
            migrationBuilder.Sql("DROP VIEW [VisitedStates];");

            // Step 5: Remove per-user columns from Places — Places are now global
            migrationBuilder.DropForeignKey(
                name: "FK_Places_Users_UserId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_UserId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IsHome",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Places");

            // Step 6: Recreate VisitedStates VIEW using the UserPlaces join
            migrationBuilder.Sql(@"
CREATE VIEW [VisitedStates] AS
SELECT CAST(ROW_NUMBER() OVER (ORDER BY sub.UserId, sub.CountryId, sub.StateAbbr) AS int) AS Id,
    sub.UserId, sub.CountryId, sub.StateAbbr, sub.StateName
FROM (
    SELECT up.UserId, p.CountryId, p.StateAbbr, MAX(p.StateName) AS StateName
    FROM Places p
    JOIN UserPlaces up ON up.PlaceId = p.Id
    WHERE p.StateAbbr IS NOT NULL
    GROUP BY up.UserId, p.CountryId, p.StateAbbr
) AS sub;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new VIEW that depends on UserPlaces
            migrationBuilder.Sql("DROP VIEW [VisitedStates];");

            // Restore Places.UserId and Places.IsHome columns
            migrationBuilder.AddColumn<bool>(
                name: "IsHome",
                table: "Places",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Places",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Restore data from UserPlaces back to Places (best-effort; deduplication is not reversible)
            migrationBuilder.Sql(@"
UPDATE p
SET p.UserId = up.UserId, p.IsHome = up.IsHome
FROM Places p
JOIN UserPlaces up ON up.PlaceId = p.Id;
");

            migrationBuilder.DropTable(name: "UserPlaces");

            migrationBuilder.CreateIndex(
                name: "IX_Places_UserId",
                table: "Places",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Users_UserId",
                table: "Places",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Restore original VisitedStates VIEW
            migrationBuilder.Sql(@"
CREATE VIEW [VisitedStates] AS
SELECT CAST(ROW_NUMBER() OVER (ORDER BY p.UserId, p.CountryId, p.StateAbbr) AS int) AS Id,
    p.UserId, p.CountryId, p.StateAbbr, p.StateName
FROM (
    SELECT UserId, CountryId, StateAbbr, MAX(StateName) AS StateName
    FROM Places
    WHERE StateAbbr IS NOT NULL
    GROUP BY UserId, CountryId, StateAbbr
) AS p;
");
        }
    }
}
