using System.Text;

public sealed class IniConfigProvider : IConfigProvider
{
    public string SourceName => "ini";
    public bool SupportsHotReload => true;

    public IDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("INI file not found", path);
        var lines = File.ReadAllLines(path, new UTF8Encoding(false));
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? section = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith(";") || line.StartsWith("#")) continue;

            if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2)
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim();
            var value = line[(eq + 1)..].Trim();
            var full = string.IsNullOrEmpty(section) ? key : section + ":" + key;
            dict[full] = value;
        }
        return dict;
    }
}
