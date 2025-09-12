using System.Text;
using System.Text.Json;

public sealed class JsonConfigProvider : IConfigProvider
{
    public string SourceName => "json";
    public bool SupportsHotReload => true;

    public IDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("JSON file not found", path);
        var json = File.ReadAllText(path, new UTF8Encoding(false));
        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("JSON root must be an object.");
        WalkJson(doc.RootElement, dict, prefix: null);
        return dict;
    }

    private void WalkJson(JsonElement el, Dictionary<string, string> dict, string? prefix)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    WalkJson(prop.Value, dict, Concat(prefix, prop.Name));
                }
                break;
            case JsonValueKind.Array:
                // Arrays are not supported for typed access; store JSON string for leaf
                dict[prefix ?? string.Empty] = el.GetRawText();
                break;
            case JsonValueKind.String:
                dict[prefix ?? string.Empty] = el.GetString() ?? string.Empty; break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                dict[prefix ?? string.Empty] = el.GetRawText(); break;
            default:
                throw new InvalidDataException($"Unsupported JSON kind: {el.ValueKind}");
        }
    }

    private static string Concat(string? prefix, string name)
        => string.IsNullOrEmpty(prefix) ? name : prefix + ":" + name;
}
