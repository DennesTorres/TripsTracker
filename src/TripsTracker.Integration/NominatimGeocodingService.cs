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
        { "city", "town", "village", "municipality", "hamlet", "suburb", "locality", "quarter" };

    private static readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions _compareOpts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    // Photon (photon.komoot.io) is an autocomplete API built on OSM data.
    // Unlike Nominatim /search, it returns prefix matches for partial city names.
    private const string PhotonBaseUrl = "https://photon.komoot.io";

    public async Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, int limit = 5, string countryCode = "", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        // Photon has no server-side country filter — fetch more and filter client-side.
        var url = $"{PhotonBaseUrl}/api/?q={Uri.EscapeDataString(query)}&lang=en&limit={limit * 10}";
        var response = await _http.GetFromJsonAsync<PhotonFeatureCollection>(url, ct);
        if (response?.Features is null or { Length: 0 })
            return [];

        var normalizedCountry = countryCode.ToUpperInvariant();
        return response.Features
            .Where(f =>
            {
                var props = f.Properties;
                if (props?.CountryCode is null || props.Name is null) return false;
                if (!CityTypes.Contains(props.OsmValue ?? "")) return false;
                if (!string.IsNullOrWhiteSpace(normalizedCountry) && !props.CountryCode.Equals(normalizedCountry, StringComparison.OrdinalIgnoreCase)) return false;
                return _compareInfo.IndexOf(props.Name, query, _compareOpts) == 0;
            })
            .DistinctBy(f => (f.Properties!.CountryCode, f.Properties.Name))
            .Take(limit)
            .Select(f =>
            {
                var props = f.Properties!;
                var countryIso = props.CountryCode!.ToUpperInvariant();
                // GeoJSON: coordinates[0] = longitude, coordinates[1] = latitude
                var coords = f.Geometry?.Coordinates;
                return new CitySuggestion(
                    props.Name!, props.Country ?? countryIso, countryIso, props.State, null,
                    Lat: coords?.Length >= 2 ? coords[1] : null,
                    Lon: coords?.Length >= 2 ? coords[0] : null);
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

    // ─── Nominatim response models (GeocodeAsync) ────────────────────────────

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]          public string Lat         { get; set; } = string.Empty;
        [JsonPropertyName("lon")]          public string Lon         { get; set; } = string.Empty;
        [JsonPropertyName("name")]         public string? Name        { get; set; }
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

    // ─── Photon response models (SuggestCitiesAsync) ──────────────────────────

    private sealed class PhotonFeatureCollection
    {
        [JsonPropertyName("features")] public PhotonFeature[]? Features { get; set; }
    }

    private sealed class PhotonFeature
    {
        [JsonPropertyName("geometry")]   public PhotonGeometry?   Geometry   { get; set; }
        [JsonPropertyName("properties")] public PhotonProperties? Properties { get; set; }
    }

    private sealed class PhotonGeometry
    {
        // GeoJSON point geometry: coordinates[0] = longitude, coordinates[1] = latitude
        [JsonPropertyName("coordinates")] public double[]? Coordinates { get; set; }
    }

    private sealed class PhotonProperties
    {
        [JsonPropertyName("name")]        public string? Name        { get; set; }
        [JsonPropertyName("osm_value")]   public string? OsmValue    { get; set; }
        [JsonPropertyName("countrycode")] public string? CountryCode { get; set; }
        [JsonPropertyName("country")]     public string? Country     { get; set; }
        [JsonPropertyName("state")]       public string? State       { get; set; }
    }
}
