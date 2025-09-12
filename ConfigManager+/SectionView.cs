public sealed class SectionView
{
    private readonly ConfigManager _mgr;
    private readonly string _prefix; // normalized ends with ':'
    internal SectionView(ConfigManager mgr, string prefix)
    {
        _mgr = mgr; _prefix = prefix;
    }

    private string Key(string key) => string.Concat(_prefix, key);

    // Delegating API
    public string? Get(string key) => _mgr.Get(Key(key));
    public string Get(string key, string def) => _mgr.Get(Key(key), def);
    public int GetInt(string key, int def = 0) => _mgr.GetInt(Key(key), def);
    public long GetLong(string key, long def = 0) => _mgr.GetLong(Key(key), def);
    public bool GetBool(string key, bool def = false) => _mgr.GetBool(Key(key), def);
    public double GetDouble(string key, double def = 0) => _mgr.GetDouble(Key(key), def);
    public TimeSpan GetTimeSpan(string key, TimeSpan def) => _mgr.GetTimeSpan(Key(key), def);
    public Guid GetGuid(string key, Guid def) => _mgr.GetGuid(Key(key), def);
    public SectionView Section(string child) => _mgr.Section(_prefix + child);
    public T Bind<T>() where T : new() => _mgr.Bind<T>(_prefix.TrimEnd(':'));
    public override string ToString() => _prefix; // debug
}
