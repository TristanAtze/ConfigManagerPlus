using System.Text;

using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// ConfigManager+ core. Compose providers as layers; last added wins.
/// </summary>
public sealed class ConfigManager : IDisposable
{
    private readonly List<ConfigLayer> _layers = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly char _sep = ':';
    private Dictionary<string, string> _merged = new(StringComparer.OrdinalIgnoreCase);
    private int _orderCounter = 0;

    // Secret masking keywords (case-insensitive substring match)
    private static readonly string[] SecretHints = new[] { "password", "pwd", "secret", "token", "apikey", "api_key", "key", "private", "connectionstring" };

    public event EventHandler<ConfigChangedEventArgs>? Changed;
    public event EventHandler<Exception>? Error;

    public ConfigManager() { }

    // ------------ Add sources ------------

    public ConfigManager AddJson(string path, bool reloadOnChange = true)
        => AddFileProvider(path, new JsonConfigProvider(), reloadOnChange);

    public ConfigManager AddYaml(string path, bool reloadOnChange = true)
        => AddFileProvider(path, new YamlConfigProvider(), reloadOnChange);

    public ConfigManager AddIni(string path, bool reloadOnChange = true)
        => AddFileProvider(path, new IniConfigProvider(), reloadOnChange);

    public ConfigManager AddEnvFile(string path = ".env", bool reloadOnChange = true)
        => AddFileProvider(path, new EnvFileConfigProvider(), reloadOnChange);

    private ConfigManager AddFileProvider(string path, IConfigProvider provider, bool reloadOnChange)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
        var full = System.IO.Path.GetFullPath(path);
        var data = provider.Load(full);

        var layer = new ConfigLayer(full, provider, ++_orderCounter, false, data);

        _lock.EnterWriteLock();
        try
        {
            _layers.Add(layer);
            RebuildMerged_NoLock(out _, out _);
        }
        finally { _lock.ExitWriteLock(); }

        if (reloadOnChange && provider.SupportsHotReload)
        {
            TryAttachWatcher(layer);
        }
        return this;
    }

    /// <summary>
    /// Apply environment variables as an override layer. If prefix is provided, only variables starting with that prefix
    /// are considered, and the prefix is removed. Double underscore "__" is treated as section separator.
    /// Example: APP__Database__Port=5432 -> key "Database:Port".
    /// </summary>
    public ConfigManager AddEnvironmentVariables(string? prefix = null)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
        {
            var key = de.Key?.ToString();
            var val = de.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(key)) continue;
            if (!string.IsNullOrEmpty(prefix))
            {
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                key = key.Substring(prefix.Length);
            }

            key = key.Replace("__", _sep.ToString());
            key = key.TrimStart(_sep);
            if (key.Length == 0) continue;
            dict[key] = val;
        }

        AddDynamicLayer("<EnvironmentVariables>", new PassthroughProvider("env"), dict);
        return this;
    }

    /// <summary>
    /// Apply command-line overrides. Supports: --Key=value or --Section:Key=value or "--Key value" (space separated).
    /// Keys may use ':' to denote sections. Single dash is also accepted.
    /// </summary>
    public ConfigManager AddCommandLine(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!(a.StartsWith("--") || a.StartsWith("-"))) continue;

            string keyval = a.TrimStart('-');
            string key, value;

            int eq = keyval.IndexOf('=');
            if (eq >= 0)
            {
                key = keyval.Substring(0, eq).Trim();
                value = keyval[(eq + 1)..].Trim();
            }
            else
            {
                key = keyval.Trim();
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    value = args[++i];
                }
                else
                {
                    value = "true"; // flag
                }
            }

            if (string.IsNullOrEmpty(key)) continue;
            dict[key] = value;
        }

        AddDynamicLayer("<CommandLine>", new PassthroughProvider("args"), dict);
        return this;
    }

    private void AddDynamicLayer(string name, IConfigProvider provider, IDictionary<string, string> dict)
    {
        var layer = new ConfigLayer(name, provider, ++_orderCounter, true, dict);
        _lock.EnterWriteLock();
        try
        {
            _layers.Add(layer);
            RebuildMerged_NoLock(out _, out _);
        }
        finally { _lock.ExitWriteLock(); }
    }

    private void TryAttachWatcher(ConfigLayer layer)
    {
        try
        {
            string dir = System.IO.Path.GetDirectoryName(layer.Path)!;
            string file = System.IO.Path.GetFileName(layer.Path);
            var watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            watcher.Changed += async (s, e) => await OnFileChanged(layer, e).ConfigureAwait(false);
            watcher.Created += async (s, e) => await OnFileChanged(layer, e).ConfigureAwait(false);
            watcher.Deleted += async (s, e) => await OnFileChanged(layer, e).ConfigureAwait(false);
            watcher.Renamed += async (s, e) => await OnFileChanged(layer, e).ConfigureAwait(false);
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;
            layer.Watcher = watcher;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    private async Task OnFileChanged(ConfigLayer layer, FileSystemEventArgs e)
    {
        try
        {
            // debounce small bursts
            await Task.Delay(50).ConfigureAwait(false);
            var fresh = layer.Provider.Load(layer.Path);
            Dictionary<string, string> before, after;

            _lock.EnterWriteLock();
            try
            {
                layer.Data = fresh;
                before = new Dictionary<string, string>(_merged, _merged.Comparer);
                RebuildMerged_NoLock(out _, out _);
                after = new Dictionary<string, string>(_merged, _merged.Comparer);
            }
            finally { _lock.ExitWriteLock(); }

            var (added, modified, removed) = Diff(before, after);
            if (added.Count > 0 || modified.Count > 0 || removed.Count > 0)
            {
                Changed?.Invoke(this, new ConfigChangedEventArgs(added, modified, removed, layer.Path, layer.Provider.SourceName));
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    private static (Dictionary<string, string> Added, Dictionary<string, (string OldValue, string NewValue)> Modified, List<string> Removed)
        Diff(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        var added = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var modified = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        var removed = new List<string>();

        foreach (var kv in after)
        {
            if (!before.TryGetValue(kv.Key, out var oldv))
                added[kv.Key] = kv.Value;
            else if (!string.Equals(oldv, kv.Value, StringComparison.Ordinal))
                modified[kv.Key] = (oldv, kv.Value);
        }
        foreach (var kv in before)
        {
            if (!after.ContainsKey(kv.Key)) removed.Add(kv.Key);
        }
        return (added, modified, removed);
    }

    private void RebuildMerged_NoLock(out int keys, out int layers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in _layers.OrderBy(l => l.Order))
        {
            foreach (var kv in layer.Data)
            {
                result[kv.Key] = kv.Value;
            }
        }
        _merged = result;
        keys = result.Count;
        layers = _layers.Count;
    }

    // ------------ Public API ------------

    public int Count
    {
        get { _lock.EnterReadLock(); try { return _merged.Count; } finally { _lock.ExitReadLock(); } }
    }

    public bool ContainsKey(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _lock.EnterReadLock();
        try { return _merged.ContainsKey(key); }
        finally { _lock.ExitReadLock(); }
    }

    public string? Get(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _lock.EnterReadLock();
        try { return _merged.TryGetValue(key, out var v) ? v : null; }
        finally { _lock.ExitReadLock(); }
    }

    public string Get(string key, string defaultValue)
        => Get(key) ?? defaultValue;

    // Typed getters
    public int GetInt(string key, int @default = 0)
        => TryParse(key, int.TryParse, @default);

    public long GetLong(string key, long @default = 0)
        => TryParse(key, long.TryParse, @default);

    public bool GetBool(string key, bool @default = false)
        => TryParse(key, TryParseBool, @default);

    public double GetDouble(string key, double @default = 0)
        => TryParse(key, (string s, out double v) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v), @default);

    public TimeSpan GetTimeSpan(string key, TimeSpan @default)
        => TryParse(key, TimeSpan.TryParse, @default);

    public Guid GetGuid(string key, Guid @default)
        => TryParse(key, Guid.TryParse, @default);

    private T TryParse<T>(string key, TryParseHandler<T> parser, T @default)
    {
        var s = Get(key);
        if (s is null) return @default;
        return parser(s, out var val) ? val : @default;
    }
    private delegate bool TryParseHandler<T>(string s, out T value);
    private static bool TryParseBool(string s, out bool v)
    {
        s = s.Trim();
        if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "on", StringComparison.OrdinalIgnoreCase)) { v = true; return true; }
        if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "off", StringComparison.OrdinalIgnoreCase)) { v = false; return true; }
        return bool.TryParse(s, out v);
    }

    // Sections
    public SectionView Section(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        return new SectionView(this, NormalizePrefix(path));
    }

    private string NormalizePrefix(string path)
    {
        var p = path.Replace("__", _sep.ToString());
        return p.EndsWith(_sep) ? p : p + _sep;
    }

    // Validation
    public void Require(params string[] keys)
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));
        var missing = new List<string>();
        _lock.EnterReadLock();
        try
        {
            foreach (var k in keys)
            {
                if (!_merged.ContainsKey(k)) missing.Add(k);
            }
        }
        finally { _lock.ExitReadLock(); }

        if (missing.Count > 0)
            throw new InvalidOperationException("Missing required configuration key(s): " + string.Join(", ", missing));
    }

    // Snapshot
    public IReadOnlyDictionary<string, string> Snapshot()
    {
        _lock.EnterReadLock();
        try { return new Dictionary<string, string>(_merged, _merged.Comparer); }
        finally { _lock.ExitReadLock(); }
    }

    // Pretty dump
    public string Dump(bool maskSecrets = true)
    {
        var sb = new StringBuilder();
        _lock.EnterReadLock();
        try
        {
            foreach (var kv in _merged.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var v = maskSecrets && IsSecretKey(kv.Key) ? Mask(kv.Value) : kv.Value;
                sb.Append(kv.Key).Append(" = ").AppendLine(v ?? string.Empty);
            }
        }
        finally { _lock.ExitReadLock(); }
        return sb.ToString();
    }

    private static bool IsSecretKey(string key)
        => SecretHints.Any(h => key.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
    private static string Mask(string? v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        if (v!.Length <= 4) return new string('*', v.Length);
        return new string('*', Math.Max(0, v.Length - 4)) + v[^4..];
    }

    // Bind to POCO using JSON round-trip
    public T Bind<T>(string? section = null) where T : new()
    {
        var tree = BuildTree(section);
        var json = JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false });
        var obj = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return obj ?? new T();
    }

    private Dictionary<string, object?> BuildTree(string? section)
    {
        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        string prefix = section is null ? string.Empty : NormalizePrefix(section);

        _lock.EnterReadLock();
        try
        {
            foreach (var kv in _merged)
            {
                if (prefix.Length > 0 && !kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var path = (prefix.Length == 0) ? kv.Key : kv.Key.Substring(prefix.Length);
                InsertPath(root, path.Split(_sep), kv.Value);
            }
        }
        finally { _lock.ExitReadLock(); }

        return root;
    }

    private static void InsertPath(Dictionary<string, object?> node, IReadOnlyList<string> parts, string? value)
    {
        var current = node;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (i == parts.Count - 1)
            {
                current[p] = value;
            }
            else
            {
                if (!current.TryGetValue(p, out var child) || child is not Dictionary<string, object?> dict)
                {
                    dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    current[p] = dict;
                }
                current = dict;
            }
        }
    }

    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var l in _layers)
            {
                l.Watcher?.Dispose();
            }
            _layers.Clear();
            _merged.Clear();
        }
        finally { _lock.ExitWriteLock(); }
        _lock.Dispose();
    }
}
