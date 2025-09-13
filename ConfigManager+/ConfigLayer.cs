namespace ConfigManagerPlus;

/// <summary>
/// Represents one configured source layer (file/env/args) with precedence and optional watcher.
/// </summary>
internal sealed class ConfigLayer
{
    public string Path { get; }
    public IConfigProvider Provider { get; }
    public int Order { get; } // higher order = higher precedence
    public FileSystemWatcher? Watcher { get; set; }
    public IDictionary<string, string> Data { get; set; }
    public bool IsDynamic { get; } // env/args

    public ConfigLayer(string path, IConfigProvider provider, int order, bool isDynamic, IDictionary<string, string> data)
    {
        Path = path;
        Provider = provider;
        Order = order;
        IsDynamic = isDynamic;
        Data = data;
    }
}

