using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUkFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The UK flag was stored as the England subdivision emoji (🏴󠁧󠁢󠁥󠁮󠁧󠁿) — 28 chars of proper Unicode
            // surrogate pairs — which Windows cannot render (shows a grey striped box).
            // All other country flags are stored as UTF-8 mojibake (8 chars each), decoded
            // back to emoji by the frontend's cp1252.ts decoder.
            //
            // 🇬🇧 = 🇬 (U+1F1EC) + 🇧 (U+1F1E7)
            // UTF-8 bytes → CP1252 decode:
            //   F0 → NCHAR(240) = ð
            //   9F → NCHAR(376) = Ÿ  (CP1252 0x9F → U+0178)
            //   87 → NCHAR(8225) = ‡  (CP1252 0x87 → U+2021)
            //   AC → NCHAR(172) = ¬
            //   F0 → NCHAR(240) = ð
            //   9F → NCHAR(376) = Ÿ
            //   87 → NCHAR(8225) = ‡
            //   A7 → NCHAR(167) = §
            migrationBuilder.Sql("""
                UPDATE Countries
                SET Flag = NCHAR(240) + NCHAR(376) + NCHAR(8225) + NCHAR(172)
                         + NCHAR(240) + NCHAR(376) + NCHAR(8225) + NCHAR(167)
                WHERE IsoAlpha2 = 'GB';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
