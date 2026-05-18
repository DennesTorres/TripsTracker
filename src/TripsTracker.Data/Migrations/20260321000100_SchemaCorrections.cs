using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Places: add CountryId FK ───────────────────────────────────────────
            migrationBuilder.Sql("ALTER TABLE Places ADD CountryId int NULL");

            // Normalise known country name aliases before resolving CountryId
            migrationBuilder.Sql("UPDATE Places SET CountryName = 'United States' WHERE CountryName = 'USA'");

            migrationBuilder.Sql(@"
UPDATE p SET p.CountryId = c.Id
FROM Places p
JOIN Countries c ON c.Name = p.CountryName AND c.IsDeleted = 0
WHERE p.CountryId IS NULL");

            // ── Places: extract StateAbbr from City "(XX)" pattern ────────────────
            migrationBuilder.Sql("ALTER TABLE Places ADD StateAbbr nvarchar(10) NULL");

            migrationBuilder.Sql(@"
UPDATE Places
SET StateAbbr = SUBSTRING(City, CHARINDEX('(', City)+1, CHARINDEX(')', City) - CHARINDEX('(', City) - 1)
WHERE City LIKE '%(%)%'
AND LEN(SUBSTRING(City, CHARINDEX('(', City)+1, CHARINDEX(')', City) - CHARINDEX('(', City) - 1)) <= 3");

            migrationBuilder.Sql(@"
UPDATE Places
SET City = RTRIM(SUBSTRING(City, 1, CHARINDEX('(', City)-1))
WHERE City LIKE '%(%)%'
AND LEN(SUBSTRING(City, CHARINDEX('(', City)+1, CHARINDEX(')', City) - CHARINDEX('(', City) - 1)) <= 3");

            // ── Places: drop old string columns ───────────────────────────────────
            migrationBuilder.DropIndex(name: "IX_Places_CountryName", table: "Places");
            migrationBuilder.DropColumn(name: "CountryName", table: "Places");
            migrationBuilder.DropColumn(name: "Flag", table: "Places");

            // ── Places: make CountryId NOT NULL, add index ─────────────────────
            migrationBuilder.Sql("ALTER TABLE Places ALTER COLUMN CountryId int NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_Places_CountryId", table: "Places", column: "CountryId");

            // ── VisitedStates: add CountryId FK ───────────────────────────────────
            migrationBuilder.Sql("ALTER TABLE VisitedStates ADD CountryId int NULL");

            migrationBuilder.Sql(@"
UPDATE vs SET vs.CountryId = c.Id
FROM VisitedStates vs
JOIN Countries c ON c.IsoAlpha2 = vs.CountryCode AND c.IsDeleted = 0
WHERE vs.CountryId IS NULL");

            // ── VisitedStates: drop old string column and index ───────────────────
            migrationBuilder.DropIndex(name: "IX_VisitedStates_CountryCode_StateAbbr", table: "VisitedStates");
            migrationBuilder.DropColumn(name: "CountryCode", table: "VisitedStates");

            // ── VisitedStates: make CountryId NOT NULL, add index ─────────────────
            migrationBuilder.Sql("ALTER TABLE VisitedStates ALTER COLUMN CountryId int NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_VisitedStates_CountryId_StateAbbr",
                table: "VisitedStates",
                columns: new[] { "CountryId", "StateAbbr" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse VisitedStates
            migrationBuilder.DropIndex(name: "IX_VisitedStates_CountryId_StateAbbr", table: "VisitedStates");
            migrationBuilder.AddColumn<string>(name: "CountryCode", table: "VisitedStates", type: "nvarchar(450)", nullable: false, defaultValue: "");
            migrationBuilder.Sql(@"
UPDATE vs SET vs.CountryCode = c.IsoAlpha2
FROM VisitedStates vs JOIN Countries c ON c.Id = vs.CountryId");
            migrationBuilder.DropColumn(name: "CountryId", table: "VisitedStates");
            migrationBuilder.CreateIndex(name: "IX_VisitedStates_CountryCode_StateAbbr", table: "VisitedStates", columns: new[] { "CountryCode", "StateAbbr" }, unique: true);

            // Reverse Places
            migrationBuilder.DropIndex(name: "IX_Places_CountryId", table: "Places");
            migrationBuilder.AddColumn<string>(name: "CountryName", table: "Places", type: "nvarchar(450)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Flag", table: "Places", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.Sql(@"
UPDATE p SET p.CountryName = c.Name, p.Flag = c.Flag
FROM Places p JOIN Countries c ON c.Id = p.CountryId");
            migrationBuilder.DropColumn(name: "StateAbbr", table: "Places");
            migrationBuilder.DropColumn(name: "CountryId", table: "Places");
            migrationBuilder.CreateIndex(name: "IX_Places_CountryName", table: "Places", column: "CountryName");
        }
    }
}
