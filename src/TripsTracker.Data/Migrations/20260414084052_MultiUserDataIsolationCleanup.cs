using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiUserDataIsolationCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the VisitedStates VIEW before altering Places.UserId (view references the column)
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");

            migrationBuilder.DropColumn(
                name: "IsHome",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "IsVisited",
                table: "Countries");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Places",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_CountryId",
                table: "UserCountries",
                column: "CountryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Countries_CountryId",
                table: "Places",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Users_UserId",
                table: "Places",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCountries_Countries_CountryId",
                table: "UserCountries",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCountries_Users_UserId",
                table: "UserCountries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Recreate VisitedStates VIEW with non-nullable UserId
            migrationBuilder.Sql(@"
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
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Places_Countries_CountryId",
                table: "Places");

            migrationBuilder.DropForeignKey(
                name: "FK_Places_Users_UserId",
                table: "Places");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCountries_Countries_CountryId",
                table: "UserCountries");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCountries_Users_UserId",
                table: "UserCountries");

            migrationBuilder.DropIndex(
                name: "IX_UserCountries_CountryId",
                table: "UserCountries");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Places",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsHome",
                table: "Countries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisited",
                table: "Countries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
