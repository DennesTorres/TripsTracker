using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixCompoundCityEncoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The original FixDataQuality migration searched for the proper Unicode em-dash (U+2014).
            // The actual stored data has UTF-8 em-dash bytes (0xE2 0x80 0x94) misread as Windows-1252:
            //   NCHAR(226) = â, NCHAR(8364) = €, NCHAR(8221) = "
            // The separator pattern in the data is: SPACE + â€" + SPACE (5 chars total).

            // Split compound cities where StateName has not yet been set.
            // Example: 'Kefalonia â€" Agia Efimia' → City='Agia Efimia', StateName='Kefalonia'
            migrationBuilder.Sql("""
                DECLARE @sep NVARCHAR(10) = N' ' + NCHAR(226) + NCHAR(8364) + NCHAR(8221) + N' ';
                UPDATE Places
                SET StateName = TRIM(SUBSTRING(City, 1, CHARINDEX(@sep, City) - 1)),
                    City      = TRIM(SUBSTRING(City, CHARINDEX(@sep, City) + 5, LEN(City)))
                WHERE City LIKE N'%' + NCHAR(226) + NCHAR(8364) + NCHAR(8221) + N'%'
                  AND StateName IS NULL;
                """);

            // For entries where StateName was already set, only strip the compound prefix from City.
            // Example: City='Sergipe â€" Xingó border', StateName='Sergipe' → City='Xingó border'
            migrationBuilder.Sql("""
                DECLARE @sep NVARCHAR(10) = N' ' + NCHAR(226) + NCHAR(8364) + NCHAR(8221) + N' ';
                UPDATE Places
                SET City = TRIM(SUBSTRING(City, CHARINDEX(@sep, City) + 5, LEN(City)))
                WHERE City LIKE N'%' + NCHAR(226) + NCHAR(8364) + NCHAR(8221) + N'%'
                  AND StateName IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Compound city splits are not reversible once StateName is set.
        }
    }
}
