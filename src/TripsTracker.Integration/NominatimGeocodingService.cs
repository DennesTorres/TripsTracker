using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Integration;

public class NominatimGeocodingService : INominatimService
{
    private readonly HttpClient _http;

    public NominatimGeocodingService(HttpClient http)
    {
        _http = http;
    }

    private static readonly HashSet<string> CityTypes = new(StringComparer.OrdinalIgnoreCase)
        { "city", "town", "village", "municipality", "hamlet" };

    private static readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions _compareOpts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public async Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        var url = $"/search?q={Uri.EscapeDataString(query)}&format=json&addressdetails=1&limit={limit * 3}";
        var results = await _http.GetFromJsonAsync<NominatimResult[]>(url, ct);
        if (results is null or { Length: 0 })
            return [];

        return results
            .Where(r =>
            {
                if (r.Address?.CountryCode is null) return false;
                if (r.Type is not null && !CityTypes.Contains(r.Type)
                    && (r.AddressType is null || !CityTypes.Contains(r.AddressType))) return false;
                // Only include results whose city name actually starts with the query (accent + case insensitive)
                var addr = r.Address;
                var city = addr.City ?? addr.Town ?? addr.Village ?? addr.Municipality;
                return city is not null && _compareInfo.IndexOf(city, query, _compareOpts) == 0;
            })
            .DistinctBy(r => (r.Address!.CountryCode, r.Address.City ?? r.Address.Town ?? r.Address.Village ?? r.Address.Municipality))
            .Take(limit)
            .Select(r =>
            {
                var address = r.Address!;
                var city = address.City ?? address.Town ?? address.Village ?? address.Municipality ?? query;
                var rawState = address.StateCode ?? address.Iso3166Lvl4;
                var stateAbbr = rawState?.ToUpperInvariant() is { } s
                    ? (s.Contains('-') ? s[(s.IndexOf('-') + 1)..] : s)
                    : null;
                var countryCode = address.CountryCode!.ToUpperInvariant();
                return new CitySuggestion(city, address.Country ?? countryCode, countryCode, address.State, stateAbbr);
            })
            .ToList();
    }

    public async Task<GeocodingResult?> GeocodeAsync(string cityName, string countryIsoAlpha2Hint, CancellationToken ct = default)
    {
        var url = $"/search?q={Uri.EscapeDataString(cityName)}&countrycodes={countryIsoAlpha2Hint.ToLowerInvariant()}&format=json&addressdetails=1&limit=1";

        var results = await _http.GetFromJsonAsync<NominatimResult[]>(url, ct);
        if (results is null or { Length: 0 })
            return null;

        var r = results[0];
        var address = r.Address;

        var city = address?.City
            ?? address?.Town
            ?? address?.Village
            ?? address?.Municipality
            ?? cityName;

        var rawState = address?.StateCode ?? address?.Iso3166Lvl4;
        var stateAbbr = rawState?.ToUpperInvariant() is { } s
            ? (s.Contains('-') ? s[(s.IndexOf('-') + 1)..] : s)
            : null;
        var stateName = address?.State;
        var countryCode = address?.CountryCode?.ToUpperInvariant() ?? countryIsoAlpha2Hint.ToUpperInvariant();

        return new GeocodingResult(
            Lat: double.Parse(r.Lat, System.Globalization.CultureInfo.InvariantCulture),
            Lon: double.Parse(r.Lon, System.Globalization.CultureInfo.InvariantCulture),
            City: city,
            StateAbbr: stateAbbr,
            StateName: stateName,
            CountryIsoAlpha2: countryCode);
    }

    // ─── Nominatim response models ────────────────────────────────────────────

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]          public string Lat         { get; set; } = string.Empty;
        [JsonPropertyName("lon")]          public string Lon         { get; set; } = string.Empty;
        [JsonPropertyName("type")]         public string? Type        { get; set; }
        [JsonPropertyName("addresstype")] public string? AddressType { get; set; }
        [JsonPropertyName("address")]      public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("city")]         public string? City         { get; set; }
        [JsonPropertyName("town")]         public string? Town         { get; set; }
        [JsonPropertyName("village")]      public string? Village      { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }
        [JsonPropertyName("state")]             public string? State        { get; set; }
        [JsonPropertyName("state_code")]        public string? StateCode    { get; set; }
        [JsonPropertyName("ISO3166-2-lvl4")]    public string? Iso3166Lvl4  { get; set; }
        [JsonPropertyName("country")]            public string? Country      { get; set; }
        [JsonPropertyName("country_code")]      public string? CountryCode  { get; set; }
    }
}
