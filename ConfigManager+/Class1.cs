namespace ConfigManagerPlus;

public class Class1
{
        // ------------------- USAGE EXAMPLE (Console) -------------------
        // var cfg = new ConfigManagerPlus.ConfigManager()
        //     .AddJson("appsettings.json")
        //     .AddYaml("config.yaml")
        //     .AddIni("settings.ini")
        //     .AddEnvFile(".env")
        //     .AddEnvironmentVariables("APP__")
        //     .AddCommandLine(args);
        //
        // cfg.Changed += (s, e) =>
        // {
        //     Console.WriteLine($"Config changed from {e.SourceName}::{e.SourcePath}");
        // };
        //
        // cfg.Require("Database:ConnectionString", "Server:Port");
        // int port = cfg.GetInt("Server:Port", 8080);
        // bool debug = cfg.GetBool("Server:Debug", false);
        // var db = cfg.Section("Database").Bind<MyDbSettings>();
        // Console.WriteLine(cfg.Dump());
        //
        // Console.ReadKey();
}
