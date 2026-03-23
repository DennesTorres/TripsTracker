using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStateName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StateName",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true);

            // Backfill StateName for Brazilian states
            migrationBuilder.Sql("""
                UPDATE Places SET StateName =
                    CASE StateAbbr
                        WHEN 'AC' THEN 'Acre'
                        WHEN 'AL' THEN 'Alagoas'
                        WHEN 'AP' THEN 'Amapá'
                        WHEN 'AM' THEN 'Amazonas'
                        WHEN 'BA' THEN 'Bahia'
                        WHEN 'CE' THEN 'Ceará'
                        WHEN 'DF' THEN 'Distrito Federal'
                        WHEN 'ES' THEN 'Espírito Santo'
                        WHEN 'GO' THEN 'Goiás'
                        WHEN 'MA' THEN 'Maranhão'
                        WHEN 'MT' THEN 'Mato Grosso'
                        WHEN 'MS' THEN 'Mato Grosso do Sul'
                        WHEN 'MG' THEN 'Minas Gerais'
                        WHEN 'PA' THEN 'Pará'
                        WHEN 'PB' THEN 'Paraíba'
                        WHEN 'PR' THEN 'Paraná'
                        WHEN 'PE' THEN 'Pernambuco'
                        WHEN 'PI' THEN 'Piauí'
                        WHEN 'RJ' THEN 'Rio de Janeiro'
                        WHEN 'RN' THEN 'Rio Grande do Norte'
                        WHEN 'RS' THEN 'Rio Grande do Sul'
                        WHEN 'RO' THEN 'Rondônia'
                        WHEN 'RR' THEN 'Roraima'
                        WHEN 'SC' THEN 'Santa Catarina'
                        WHEN 'SP' THEN 'São Paulo'
                        WHEN 'SE' THEN 'Sergipe'
                        WHEN 'TO' THEN 'Tocantins'
                    END
                WHERE StateAbbr IS NOT NULL
                    AND LEN(StateAbbr) = 2
                    AND CountryId = (SELECT Id FROM Countries WHERE IsoAlpha2 = 'BR');
                """);

            // Backfill StateName for US states
            migrationBuilder.Sql("""
                UPDATE Places SET StateName =
                    CASE StateAbbr
                        WHEN 'AL' THEN 'Alabama'
                        WHEN 'AK' THEN 'Alaska'
                        WHEN 'AZ' THEN 'Arizona'
                        WHEN 'AR' THEN 'Arkansas'
                        WHEN 'CA' THEN 'California'
                        WHEN 'CO' THEN 'Colorado'
                        WHEN 'CT' THEN 'Connecticut'
                        WHEN 'DE' THEN 'Delaware'
                        WHEN 'FL' THEN 'Florida'
                        WHEN 'GA' THEN 'Georgia'
                        WHEN 'HI' THEN 'Hawaii'
                        WHEN 'ID' THEN 'Idaho'
                        WHEN 'IL' THEN 'Illinois'
                        WHEN 'IN' THEN 'Indiana'
                        WHEN 'IA' THEN 'Iowa'
                        WHEN 'KS' THEN 'Kansas'
                        WHEN 'KY' THEN 'Kentucky'
                        WHEN 'LA' THEN 'Louisiana'
                        WHEN 'ME' THEN 'Maine'
                        WHEN 'MD' THEN 'Maryland'
                        WHEN 'MA' THEN 'Massachusetts'
                        WHEN 'MI' THEN 'Michigan'
                        WHEN 'MN' THEN 'Minnesota'
                        WHEN 'MS' THEN 'Mississippi'
                        WHEN 'MO' THEN 'Missouri'
                        WHEN 'MT' THEN 'Montana'
                        WHEN 'NE' THEN 'Nebraska'
                        WHEN 'NV' THEN 'Nevada'
                        WHEN 'NH' THEN 'New Hampshire'
                        WHEN 'NJ' THEN 'New Jersey'
                        WHEN 'NM' THEN 'New Mexico'
                        WHEN 'NY' THEN 'New York'
                        WHEN 'NC' THEN 'North Carolina'
                        WHEN 'ND' THEN 'North Dakota'
                        WHEN 'OH' THEN 'Ohio'
                        WHEN 'OK' THEN 'Oklahoma'
                        WHEN 'OR' THEN 'Oregon'
                        WHEN 'PA' THEN 'Pennsylvania'
                        WHEN 'RI' THEN 'Rhode Island'
                        WHEN 'SC' THEN 'South Carolina'
                        WHEN 'SD' THEN 'South Dakota'
                        WHEN 'TN' THEN 'Tennessee'
                        WHEN 'TX' THEN 'Texas'
                        WHEN 'UT' THEN 'Utah'
                        WHEN 'VT' THEN 'Vermont'
                        WHEN 'VA' THEN 'Virginia'
                        WHEN 'WA' THEN 'Washington'
                        WHEN 'WV' THEN 'West Virginia'
                        WHEN 'WI' THEN 'Wisconsin'
                        WHEN 'WY' THEN 'Wyoming'
                        WHEN 'DC' THEN 'District of Columbia'
                    END
                WHERE StateAbbr IS NOT NULL
                    AND LEN(StateAbbr) = 2
                    AND CountryId = (SELECT Id FROM Countries WHERE IsoAlpha2 = 'US');
                """);

            // Recreate VisitedStates VIEW to include StateName
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert VisitedStates VIEW to version without StateName
            migrationBuilder.Sql("DROP VIEW IF EXISTS VisitedStates;");
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

            migrationBuilder.DropColumn(
                name: "StateName",
                table: "Places");
        }
    }
}
