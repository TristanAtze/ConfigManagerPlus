using System.Text;

namespace ConfigManagerPlus;

public sealed class YamlConfigProvider : IConfigProvider
{
    public string SourceName => "yaml";
    public bool SupportsHotReload => true;

    public IDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("YAML file not found", path);
        var lines = File.ReadAllLines(path, new UTF8Encoding(false));
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Simple indentation-based mapping parser (no arrays). Tabs are not allowed.
        var stack = new Stack<(int Indent, string Prefix)>();
        stack.Push((Indent: -1, Prefix: string.Empty));

        for (int ln = 0; ln < lines.Length; ln++)
        {
            var raw = lines[ln];
            if (raw.Length == 0) continue;
            if (raw.TrimStart().StartsWith("#")) continue;
            if (raw.Contains('\t')) throw new InvalidDataException($"YAML: Tabs are not allowed (line {ln + 1}).");

            int indent = CountLeadingSpaces(raw);
            var line = raw.Trim();
            if (line.Length == 0) continue;

            // Disallow lists in + (for simplicity)
            if (line.StartsWith("- "))
                throw new InvalidDataException($"YAML arrays are not supported in ConfigManager+ (line {ln + 1}).");

            // key: value OR key: (then nested)
            int colon = FindColonOutsideQuotes(line);
            if (colon <= 0)
                throw new InvalidDataException($"Invalid YAML mapping at line {ln + 1}.");

            string key = line.Substring(0, colon).Trim();
            string rest = line[(colon + 1)..].Trim();

            while (stack.Peek().Indent >= indent)
                stack.Pop();

            string prefix = stack.Peek().Prefix;
            string fullKey = string.IsNullOrEmpty(prefix) ? key : prefix + ":" + key;

            if (rest.Length == 0)
            {
                // parent mapping
                stack.Push((indent, fullKey));
            }
            else
            {
                dict[fullKey] = ParseYamlScalar(rest);
            }
        }

        return dict;
    }

    private static int CountLeadingSpaces(string s)
    {
        int i = 0; while (i < s.Length && s[i] == ' ') i++; return i;
    }
    private static int FindColonOutsideQuotes(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"' && !inS) inD = !inD; else if (c == '\'' && !inD) inS = !inS; else if (c == ':' && !inS && !inD) return i;
        }
        return -1;
    }
    private static string ParseYamlScalar(string s)
    {
        // Handle common scalars: quoted/unquoted, booleans, numbers, null
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
        {
            bool isDouble = s[0] == '"';
            var inner = s.Substring(1, s.Length - 2);
            return isDouble ? Unescape(inner) : inner;
        }
        // strip comments like value # comment
        var hash = s.IndexOf('#');
        if (hash >= 0) s = s.Substring(0, hash).TrimEnd();
        return s;
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
