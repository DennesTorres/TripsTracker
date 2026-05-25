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

    private const double SimplificationTolerance = 0.01;

    private static void WriteRewindedPolygon(Utf8JsonWriter writer, JsonElement polygon)
    {
        writer.WriteStartArray();
        foreach (var ring in polygon.EnumerateArray())
        {
            var positions = ring.EnumerateArray().ToList();
            positions.Reverse();
            var simplified = RdpSimplify(positions, SimplificationTolerance);
            if (simplified.Count < 4) simplified = positions;
            writer.WriteStartArray();
            foreach (var pos in simplified)
                pos.WriteTo(writer);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private static List<JsonElement> RdpSimplify(List<JsonElement> points, double tolerance)
    {
        if (points.Count < 3) return points;
        var keep = new bool[points.Count];
        keep[0] = true;
        keep[points.Count - 1] = true;
        RdpRecurse(points, 0, points.Count - 1, tolerance, keep);
        return points.Where((_, i) => keep[i]).ToList();
    }

    private static void RdpRecurse(List<JsonElement> points, int start, int end, double tolerance, bool[] keep)
    {
        if (end <= start + 1) return;
        double maxDist = 0;
        int maxIdx = start;
        for (int i = start + 1; i < end; i++)
        {
            var d = PerpendicularDistance(points[i], points[start], points[end]);
            if (d > maxDist) { maxDist = d; maxIdx = i; }
        }
        if (maxDist > tolerance)
        {
            keep[maxIdx] = true;
            RdpRecurse(points, start, maxIdx, tolerance, keep);
            RdpRecurse(points, maxIdx, end, tolerance, keep);
        }
    }

    private static double PerpendicularDistance(JsonElement point, JsonElement lineStart, JsonElement lineEnd)
    {
        double x = point[0].GetDouble(), y = point[1].GetDouble();
        double x1 = lineStart[0].GetDouble(), y1 = lineStart[1].GetDouble();
        double x2 = lineEnd[0].GetDouble(), y2 = lineEnd[1].GetDouble();
        double dx = x2 - x1, dy = y2 - y1;
        if (dx == 0 && dy == 0)
            return Math.Sqrt((x - x1) * (x - x1) + (y - y1) * (y - y1));
        double t = ((x - x1) * dx + (y - y1) * dy) / (dx * dx + dy * dy);
        double px = x1 + t * dx - x, py = y1 + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }

    private sealed class GeoBoundariesMetadata
    {
        [JsonPropertyName("gjDownloadURL")]
        public string? DownloadUrl { get; set; }
    }
}
