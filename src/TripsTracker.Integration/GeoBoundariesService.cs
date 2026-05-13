using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

        string rawGeoJson;
        try
        {
            rawGeoJson = await _http.GetStringAsync(metadata.DownloadUrl, ct);
        }
        catch
        {
            return null;
        }

        return NormaliseGeoJson(rawGeoJson);
    }

    // Normalises geoBoundaries GeoJSON to GADM format:
    // - Remaps properties: shapeISO → ISO_1, shapeName → NAME_1
    // - Rewinds polygon rings (geoBoundaries is spherically CW; D3 needs CCW)
    private static string? NormaliseGeoJson(string rawGeoJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawGeoJson);
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            WriteNormalisedFeatureCollection(writer, doc.RootElement);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static void WriteNormalisedFeatureCollection(Utf8JsonWriter writer, JsonElement root)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WritePropertyName("features");
        writer.WriteStartArray();
        foreach (var feature in root.GetProperty("features").EnumerateArray())
            WriteNormalisedFeature(writer, feature);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNormalisedFeature(Utf8JsonWriter writer, JsonElement feature)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        writer.WritePropertyName("properties");
        if (feature.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var prop in props.EnumerateObject())
            {
                var name = prop.Name switch
                {
                    "shapeISO" => "ISO_1",
                    "shapeName" => "NAME_1",
                    _ => prop.Name
                };
                writer.WritePropertyName(name);
                prop.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("geometry");
        if (feature.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object)
            WriteNormalisedGeometry(writer, geometry);
        else
            writer.WriteNullValue();

        writer.WriteEndObject();
    }

    private static void WriteNormalisedGeometry(Utf8JsonWriter writer, JsonElement geometry)
    {
        var type = geometry.GetProperty("type").GetString();
        writer.WriteStartObject();
        writer.WriteString("type", type);
        writer.WritePropertyName("coordinates");
        var coordinates = geometry.GetProperty("coordinates");
        switch (type)
        {
            case "Polygon":
                WriteRewindedPolygon(writer, coordinates);
                break;
            case "MultiPolygon":
                writer.WriteStartArray();
                foreach (var polygon in coordinates.EnumerateArray())
                    WriteRewindedPolygon(writer, polygon);
                writer.WriteEndArray();
                break;
            default:
                coordinates.WriteTo(writer);
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteRewindedPolygon(Utf8JsonWriter writer, JsonElement polygon)
    {
        writer.WriteStartArray();
        foreach (var ring in polygon.EnumerateArray())
        {
            var positions = ring.EnumerateArray().ToList();
            positions.Reverse();
            writer.WriteStartArray();
            foreach (var pos in positions)
                pos.WriteTo(writer);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private sealed class GeoBoundariesMetadata
    {
        [JsonPropertyName("gjDownloadURL")]
        public string? DownloadUrl { get; set; }
    }
}
