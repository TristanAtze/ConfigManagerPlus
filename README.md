# ConfigManagerPlus

A flexible and lightweight **configuration manager for .NET** with support for **JSON, YAML, INI, .env**, plus **hot reload**, **environment variables**, **command-line overrides**, typed getters, section access, validation, and secret masking.

---

## ✨ Features
- 📂 **Multiple file formats**: JSON, YAML, INI, `.env`
- 🔄 **Hot reload**: Auto-reload config on file changes
- 🌐 **Overrides**: Environment variables & command-line arguments
- 🧩 **Merging**: Combine multiple sources (last wins)
- 🎯 **Typed getters**: `GetInt`, `GetBool`, `GetDouble`, `GetGuid`, `GetTimeSpan`
- 📦 **Section API**: Access nested configs via `cfg.Section("Database")`
- ✅ **Validation**: Require specific keys
- 🛡️ **Secret masking**: Hide sensitive values when dumping
- 🪞 **Snapshot**: Immutable copy of current config
- 🔗 **Bind<T>**: Map config into POCOs

---

## 📦 Installation
```powershell
dotnet add package ConfigManagerPlus
````

---

## 🚀 Quick Start

```csharp
using ConfigManagerPlus;

// Build config with multiple sources
var cfg = new ConfigManager()
    .AddJson("appsettings.json")
    .AddYaml("config.yaml")
    .AddIni("settings.ini")
    .AddEnvFile(".env")
    .AddEnvironmentVariables("APP__") // Prefix filter
    .AddCommandLine(args);

// Typed access with defaults
int port = cfg.GetInt("Server:Port", 8080);
bool debug = cfg.GetBool("Server:Debug", false);

// Section view
var dbSection = cfg.Section("Database");
string conn = dbSection.Get("ConnectionString", "localhost");

// Validation
cfg.Require("Database:ConnectionString", "Server:Port");

// Bind to POCO
var dbSettings = cfg.Section("Database").Bind<DbSettings>();

// Pretty dump with secret masking
Console.WriteLine(cfg.Dump());
```

---

## 📂 Example Configs

### appsettings.json

```json
{
  "Server": {
    "Port": 5000,
    "Debug": true
  },
  "Database": {
    "ConnectionString": "Server=.;Database=App;Trusted_Connection=True;"
  }
}
```

### .env

```
APP__Server__Port=6000
APP__Server__Debug=false
APP__Database__ConnectionString=Server=db;User=app;Password=SuperSecret
```

---

## 🔥 Hot Reload

ConfigManager+ watches JSON, YAML, INI, and `.env` files for changes.
You can subscribe to changes:

```csharp
cfg.Changed += (s, e) =>
{
    Console.WriteLine($"Config changed from {e.SourceName}::{e.SourcePath}");
};
```

---

## 🛡️ Secret Masking

Keys matching patterns like `password`, `secret`, `token`, `apikey`, or `connectionstring` are automatically masked in `Dump()`.
The detection uses case-insensitive regular expressions and the pattern list can be customized:

```csharp
// Replace the defaults
ConfigManager.ConfigureSecretHints(new[] { "passw.*", "mysecret" });

// Add an extra pattern
ConfigManager.AddSecretHint("custom_token");
```

```
Database:ConnectionString = ************True;
ApiToken = ********abcd
```
