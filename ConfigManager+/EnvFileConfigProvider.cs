using System.Text;

public sealed class EnvFileConfigProvider : IConfigProvider
{
    public string SourceName => "env";
    public bool SupportsHotReload => true;

    public IDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(".env file not found", path);
        var lines = File.ReadAllLines(path, new UTF8Encoding(false));
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line.Substring(7).TrimStart();

            int eq = FindEqualsOutsideQuotes(line);
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim();
            var valPart = line[(eq + 1)..].Trim();
            if (key.Length == 0) continue;
            var value = ParseEnvValue(valPart);
            dict[key.Replace("__", ":")] = value;
        }
        return dict;
    }

    private static int FindEqualsOutsideQuotes(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"' && !inS) inD = !inD;
            else if (c == '\'' && !inD) inS = !inS;
            else if (c == '=' && !inS && !inD) return i;
        }
        return -1;
    }
    private static string ParseEnvValue(string part)
    {
        string trimmed = StripUnquotedComment(part);
        if (trimmed.Length >= 2 && ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            bool isDouble = trimmed[0] == '"';
            var inner = trimmed.Substring(1, trimmed.Length - 2);
            return isDouble ? Unescape(inner) : inner;
        }
        return trimmed.Trim();
    }
    private static string StripUnquotedComment(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"' && !inS) inD = !inD;
            else if (c == '\'' && !inD) inS = !inS;
            else if (c == '#' && !inS && !inD) return s.Substring(0, i).TrimEnd();
        }
        return s.Trim();
    }
    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c != '\\') { sb.Append(c); continue; }
            if (i + 1 >= s.Length) { sb.Append('\\'); break; }
            var n = s[++i];
            sb.Append(n switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                _ => n
            });
        }
        return sb.ToString();
    }
}
