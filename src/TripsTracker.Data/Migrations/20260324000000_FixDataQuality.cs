using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDataQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix "Africa — Atlantic" / "Africa/Atlantic" region variants → 'Africa'
            migrationBuilder.Sql("""
                UPDATE Countries SET Region = 'Africa'
                WHERE Region LIKE 'Africa%Atlantic%';
                """);

            // Fix Turkey: user decision — classify as 'Asia' only
            migrationBuilder.Sql("""
                UPDATE Countries SET Region = 'Asia'
                WHERE IsoAlpha2 = 'TR';
                """);

            // Fix any remaining non-standard regions (e.g. 'Home', 'Europe/Asia', etc.)
            // by mapping each country to its correct continental region.
            migrationBuilder.Sql("""
                UPDATE Countries
                SET Region = CASE
                    WHEN IsoAlpha2 IN (
                        'AG','AR','BS','BB','BZ','BO','BR','CA','CL','CO','CR','CU',
                        'DM','DO','EC','SV','GD','GT','GY','HT','HN','JM','MX','NI',
                        'PA','PY','PE','KN','LC','VC','TT','UY','US','VE','SR'
                    ) THEN 'Americas'
                    WHEN IsoAlpha2 IN (
                        'AL','AD','AT','BY','BE','BA','BG','HR','CZ','DK','EE','FI',
                        'FR','DE','GR','HU','IS','IE','IT','XK','LV','LI','LT','LU',
                        'MT','MD','MC','ME','NL','MK','NO','PL','PT','RO','RU','SM',
                        'RS','SK','SI','ES','SE','CH','UA','GB','VA'
                    ) THEN 'Europe'
                    WHEN IsoAlpha2 IN (
                        'AF','AM','AZ','BH','BD','BT','BN','KH','CN','CY','GE','IN',
                        'ID','IR','IQ','IL','JP','JO','KZ','KW','KG','LA','LB','MY',
                        'MV','MN','MM','NP','KP','OM','PK','PS','PH','QA','SA','SG',
                        'KR','LK','SY','TW','TJ','TH','TL','TM','AE','UZ','VN','YE'
                    ) THEN 'Asia'
                    WHEN IsoAlpha2 IN (
                        'DZ','AO','BJ','BW','BF','BI','CM','CV','CF','TD','KM','CD',
                        'CG','CI','DJ','EG','GQ','ER','SZ','ET','GA','GM','GH','GN',
                        'GW','KE','LS','LR','LY','MG','MW','ML','MR','MU','MA','MZ',
                        'NA','NE','NG','RW','ST','SN','SC','SL','SO','ZA','SS','SD',
                        'TZ','TG','TN','UG','ZM','ZW'
                    ) THEN 'Africa'
                    WHEN IsoAlpha2 IN (
                        'AU','FJ','KI','MH','FM','NR','NZ','PW','PG','WS','SB','TO','TV','VU'
                    ) THEN 'Oceania'
                    ELSE Region
                END
                WHERE Region NOT IN ('Africa','Americas','Asia','Europe','Oceania');
                """);

            // Fix compound city format 'StateName — City' (em dash variant)
            // Example: 'Zakynthos — Skinari' → City='Skinari', StateName='Zakynthos'
            migrationBuilder.Sql("""
                UPDATE Places
                SET StateName = TRIM(SUBSTRING(City, 1, CHARINDEX(N' — ', City) - 1)),
                    City      = TRIM(SUBSTRING(City, CHARINDEX(N' — ', City) + 3, LEN(City)))
                WHERE City LIKE N'% — %'
                  AND StateName IS NULL;
                """);

            // En dash variant (–, U+2013) — safety net
            migrationBuilder.Sql("""
                UPDATE Places
                SET StateName = TRIM(SUBSTRING(City, 1, CHARINDEX(N' – ', City) - 1)),
                    City      = TRIM(SUBSTRING(City, CHARINDEX(N' – ', City) + 3, LEN(City)))
                WHERE City LIKE N'% – %'
                  AND StateName IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Region changes are not reversible without the original values.
            // Compound city splits are not reversible once StateName is set.
        }
    }
}
