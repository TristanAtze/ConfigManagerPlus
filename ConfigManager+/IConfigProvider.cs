/// <summary>
/// Provider contract: loads a source file and returns flattened key-value pairs ("A:B:C" -> value).
/// </summary>
public interface IConfigProvider
{
    string SourceName { get; }
    IDictionary<string, string> Load(string path);
    bool SupportsHotReload { get; }
}
