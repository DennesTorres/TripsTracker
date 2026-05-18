using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorUserToEmailBasedIntId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop VIEW first — it references Places.UserId which is changing type
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");

            // Drop UserCountries — composite PK includes UserId (string); must recreate
            migrationBuilder.DropTable(name: "UserCountries");

            // Drop Places.UserId (string) — will be re-added as int
            migrationBuilder.DropIndex(name: "IX_Places_UserId", table: "Places");
            migrationBuilder.DropColumn(name: "UserId", table: "Places");

            // Drop Users — PK is string; must recreate with int IDENTITY
            migrationBuilder.DropTable(name: "Users");

            // Recreate Users with int IDENTITY PK and required Email
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "int", nullable: false)
                                       .Annotation("SqlServer:Identity", "1, 1"),
                    Email       = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt   = table.Column<DateTime>(type: "datetime2", nullable: false,
                                       defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // Re-add Places.UserId as int nullable
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Places",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_UserId",
                table: "Places",
                column: "UserId");

            // Recreate UserCountries with int UserId
            migrationBuilder.CreateTable(
                name: "UserCountries",
                columns: table => new
                {
                    UserId    = table.Column<int>(type: "int", nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    IsHome    = table.Column<bool>(type: "bit", nullable: false),
                    IsVisited = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCountries", x => new { x.UserId, x.CountryId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_UserId",
                table: "UserCountries",
                column: "UserId");

            // ── Seed: insert the owner and assign all existing data ──────────────
            // Insert the app owner. IDENTITY will assign Id = 1.
            migrationBuilder.Sql("""
                INSERT INTO Users (Email, DisplayName)
                VALUES ('dennes@bufaloinfo.com.br', 'Dennes Torres');
                """);

            // Assign all existing places to the owner
            migrationBuilder.Sql("UPDATE Places SET UserId = 1;");

            // Seed UserCountries from the legacy Countries.IsHome / IsVisited flags
            migrationBuilder.Sql("""
                INSERT INTO UserCountries (UserId, CountryId, IsHome, IsVisited)
                SELECT 1, Id, IsHome, IsVisited
                FROM Countries
                WHERE IsHome = 1 OR IsVisited = 1;
                """);

            // Recreate VisitedStates VIEW — UserId column is now int
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
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");

            migrationBuilder.DropTable(name: "UserCountries");
            migrationBuilder.DropIndex(name: "IX_Places_UserId", table: "Places");
            migrationBuilder.DropColumn(name: "UserId", table: "Places");
            migrationBuilder.DropTable(name: "Users");

            // Restore Users with string PK (no seed data — data loss acknowledged)
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id          = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email       = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt   = table.Column<DateTime>(type: "datetime2", nullable: false,
                                       defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Places",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_UserId",
                table: "Places",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "UserCountries",
                columns: table => new
                {
                    UserId    = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    IsHome    = table.Column<bool>(type: "bit", nullable: false),
                    IsVisited = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCountries", x => new { x.UserId, x.CountryId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_UserId",
                table: "UserCountries",
                column: "UserId");

            // Restore VisitedStates VIEW with string UserId
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
    }
}
