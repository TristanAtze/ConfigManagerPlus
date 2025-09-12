public sealed class ConfigChangedEventArgs : EventArgs
{
    public IReadOnlyDictionary<string, string> Added { get; }
    public IReadOnlyDictionary<string, (string OldValue, string NewValue)> Modified { get; }
    public IReadOnlyCollection<string> Removed { get; }
    public string? SourcePath { get; }
    public string? SourceName { get; }


    public ConfigChangedEventArgs(
    IDictionary<string, string> added,
    IDictionary<string, (string OldValue, string NewValue)> modified,
    IList<string> removed,
    string? sourcePath,
    string? sourceName)
    {
        Added = new Dictionary<string, string>(added);
        Modified = new Dictionary<string, (string, string)>(modified);
        Removed = new List<string>(removed);
        SourcePath = sourcePath;
        SourceName = sourceName;
    }
}