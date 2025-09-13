namespace ConfigManagerPlus;

internal sealed class PassthroughProvider : IConfigProvider
{
    public string SourceName { get; }
    public bool SupportsHotReload => false;
    public PassthroughProvider(string name) { SourceName = name; }
    public IDictionary<string, string> Load(string path) => throw new NotSupportedException();
}

