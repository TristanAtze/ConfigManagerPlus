using ConfigManagerPlus;

// Build configuration from multiple sources
var cfg = new ConfigManager()
    .AddJson("appsettings.json", optional: true, reloadOnChange: true)
    .AddYaml("config.yaml", optional: true, reloadOnChange: true)
    .AddIni("settings.ini", optional: true, reloadOnChange: true)
    .AddEnvFile(".env", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("APP__")
    .AddCommandLine(args);

// Subscribe to change events
cfg.Changed += (s, e) =>
    Console.WriteLine($"Config changed from {e.SourceName}:{e.SourcePath}");

// Ensure critical keys exist
cfg.Require("Server:Port", "Database:ConnectionString");

// Typed getters
int port = cfg.GetInt("Server:Port", 8080);
bool debug = cfg.GetBool("Server:Debug", false);

// Section access and binding
var dbSettings = cfg.Section("Database").Bind<DatabaseSettings>();

Console.WriteLine($"Port: {port}");
Console.WriteLine($"Debug: {debug}");
Console.WriteLine($"Connection: {dbSettings.ConnectionString}");

Console.WriteLine();
Console.WriteLine("Dump of current configuration:");
Console.WriteLine(cfg.Dump());

Console.WriteLine();
Console.WriteLine("Modify any config file and save to see hot reload in action. Press Enter to exit.");
Console.ReadLine();

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}
