using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Integration;

public class GeoBoundariesService : IGeoBoundariesService
{
    private const string GeoBoundariesBaseUrl = "https://www.geoboundaries.org";
    private readonly HttpClient _http;

    public GeoBoundariesService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> GetBordersAsync(string isoAlpha3, CancellationToken ct = default)
    {
        var metadataUrl = $"{GeoBoundariesBaseUrl}/api/current/gbOpen/{isoAlpha3}/ADM1/";

        HttpResponseMessage metaResponse;
        try
        {
            metaResponse = await _http.GetAsync(metadataUrl, ct);
        }
        catch
        {
            return null;
        }

        if (metaResponse.StatusCode == HttpStatusCode.NotFound) return null;
        if (!metaResponse.IsSuccessStatusCode) return null;

        var metadata = await metaResponse.Content.ReadFromJsonAsync<GeoBoundariesMetadata>(ct);
        if (string.IsNullOrEmpty(metadata?.DownloadUrl)) return null;

        try
        {
            return await _http.GetStringAsync(metadata.DownloadUrl, ct);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GeoBoundariesMetadata
    {
        [JsonPropertyName("gjDownloadURL")]
        public string? DownloadUrl { get; set; }
    }
}
