/**
 * Decodes a string that is CP1252 mojibake: UTF-8 byte sequences stored in a
 * varchar/CP1252 column, where each byte was decoded as a CP1252 Unicode code
 * point instead of being interpreted as part of a UTF-8 sequence.
 *
 * Path of corruption:
 *   "ç" (U+00E7)  →  UTF-8 bytes: C3 A7
 *   SQL Server varchar CP1252 stores raw bytes C3 A7
 *   Read back as CP1252 chars: Ã (U+00C3) + § (U+00A7)
 *   JSON arrives in JS as the string "Ã§"
 *
 * For emoji (4-byte UTF-8), bytes in 0x80–0x9F are mapped through CP1252's
 * special range (e.g. 0x9F → U+0178 Ÿ, 0x87 → U+2021 ‡), so the check
 * "all chars ≤ 0xFF" incorrectly passes/fails. This function handles that.
 *
 * Returns the corrected Unicode string, or the original if decoding fails.
 */

// Reverse map: CP1252 Unicode code point → original byte (0x80–0x9F range only)
const CP1252_REVERSE: Record<number, number> = {
  0x20AC: 0x80, // €
  0x201A: 0x82, // ‚
  0x0192: 0x83, // ƒ
  0x201E: 0x84, // „
  0x2026: 0x85, // …
  0x2020: 0x86, // †
  0x2021: 0x87, // ‡
  0x02C6: 0x88, // ˆ
  0x2030: 0x89, // ‰
  0x0160: 0x8A, // Š
  0x2039: 0x8B, // ‹
  0x0152: 0x8C, // Œ
  0x017D: 0x8E, // Ž
  0x2018: 0x91, // '
  0x2019: 0x92, // '
  0x201C: 0x93, // "
  0x201D: 0x94, // "
  0x2022: 0x95, // •
  0x2013: 0x96, // –
  0x2014: 0x97, // —
  0x02DC: 0x98, // ˜
  0x2122: 0x99, // ™
  0x0161: 0x9A, // š
  0x203A: 0x9B, // ›
  0x0153: 0x9C, // œ
  0x017E: 0x9E, // ž
  0x0178: 0x9F, // Ÿ
};

export function decodeCp1252Utf8(str: string): string {
  if (!str) return str;

  // If the string already contains genuine Unicode (code points > 0xFF that are
  // not CP1252 special chars), it is correctly encoded — leave it untouched.
  // Without this guard, emoji surrogates (e.g. 0xD83C for 🇬) would map to
  // 0x3C ('<') via `code & 0xFF`, producing corruption like '<<' for '🇬🇧'.
  const hasGenuineUnicode = [...str].some(c => {
    const cp = c.codePointAt(0)!;
    return cp > 0xFF && !(cp in CP1252_REVERSE);
  });
  if (hasGenuineUnicode) return str;

  // Map each character back to its original CP1252 byte value
  const bytes = new Uint8Array(
    [...str].map(c => {
      const code = c.charCodeAt(0);
      // ASCII and Latin-1 supplement (0xA0–0xFF) map 1:1
      if (code <= 0x7F || (code >= 0xA0 && code <= 0xFF)) return code;
      // CP1252 special range (0x80–0x9F): reverse-look up byte value
      return CP1252_REVERSE[code] ?? code & 0xFF;
    })
  );

  try {
    const decoded = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    // Accept only if the decoded string is genuinely different (shorter or has
    // higher code points), so pure-ASCII strings pass through unchanged.
    const hasHighCodePoints = [...decoded].some(c => c.codePointAt(0)! > 0x7F);
    return hasHighCodePoints || decoded.length < str.length ? decoded : str;
  } catch {
    return str; // not valid UTF-8 — leave as-is
  }
}

/** Apply decodeCp1252Utf8 to every string property of a plain object */
export function decodeStrings<T extends object>(obj: T): T {
  // Cast through Record so we can write to the spread copy without index-signature errors
  const result = { ...obj } as Record<string, unknown>;
  for (const key of Object.keys(result)) {
    if (typeof result[key] === 'string') {
      result[key] = decodeCp1252Utf8(result[key] as string);
    }
  }
  return result as unknown as T;
}
