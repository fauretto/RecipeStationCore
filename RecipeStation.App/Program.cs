using Microsoft.Extensions.Configuration;
using RecipeStation.Client;

// ── Load station configs from appsettings.json ──────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var stationConfigs = config.GetSection("Stations").Get<List<RecipeStationConfig>>()
    ?? throw new InvalidOperationException("No stations found in appsettings.json");

Console.WriteLine($"Loaded {stationConfigs.Count} station(s) from configuration:");
foreach (var s in stationConfigs)
    Console.WriteLine($"  {s.StationName}  {s.RemoteIP}  recv:{s.LocalRecvPort}  send:{s.LocalSendPort}");

// ── Build hub and subscribe to events ───────────────────────────────────────
using var hub = new RecipeStationHub(stationConfigs);

foreach (var client in hub.Clients)
{
    var name = client.StationName;
    client.LogMessage            += (_, msg) => Console.WriteLine($"[{name}] {msg}");
    client.RecipeCopiedReceived  += (_, seq) => Console.WriteLine($"[{name}] Recipe copied  SEQ:{seq:X4}");
    client.RecipeDeletedReceived += (_, seq) => Console.WriteLine($"[{name}] Recipe deleted SEQ:{seq:X4}");
    client.RecipeQuantityReceived += (_, e)  => Console.WriteLine($"[{name}] Recipe qty: {e.quantity}");
    client.CommandFailedReceived += (_, seq) => Console.WriteLine($"[{name}] Command failed SEQ:{seq:X4}");
}

// ── Go online ────────────────────────────────────────────────────────────────
hub.GoOnlineAll();
Console.WriteLine("\nAll stations online. Commands: ping <name> | copy <name> <src> <dst> | delete <name> <recipe> | count <name> | quit\n");

// ── Command loop ─────────────────────────────────────────────────────────────
while (true)
{
    var line = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(line)) continue;

    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var cmd   = parts[0].ToLower();

    if (cmd == "quit") break;

    if (parts.Length < 2) { Console.WriteLine("Usage: <command> <stationName> [args]"); continue; }

    var stationName = parts[1];
    if (!hub.StationNames.Contains(stationName))
    {
        Console.WriteLine($"Unknown station '{stationName}'. Known: {string.Join(", ", hub.StationNames)}");
        continue;
    }

    var station = hub[stationName];
    switch (cmd)
    {
        case "ping":
            station.SendPing();
            break;
        case "copy" when parts.Length >= 4:
            station.SendCopyRecipe(parts[2], parts[3]);
            break;
        case "delete" when parts.Length >= 3:
            station.SendDeleteRecipe(parts[2]);
            break;
        case "count":
            station.SendGetRecipeQuantity();
            break;
        default:
            Console.WriteLine("Unknown command or missing arguments.");
            break;
    }
}

hub.GoOfflineAll();
Console.WriteLine("Disconnected.");
