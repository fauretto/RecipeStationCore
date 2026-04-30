using Microsoft.Extensions.Configuration;
using RecipeStation.Client;

// ── Load station configs from appsettings.json ──────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var pcConfigs = config.GetSection("PCs").Get<List<PcConfig>>()
    ?? throw new InvalidOperationException("No PCs found in appsettings.json");

var stationConfigs = pcConfigs
    .SelectMany(pc => pc.Stations.Select(s => s.ToRecipeStationConfig(pc.IP)))
    .ToList();

Console.WriteLine($"Loaded {pcConfigs.Count} PC(s), {stationConfigs.Count} station(s) from configuration:");
foreach (var pc in pcConfigs)
{
    Console.WriteLine($"  [{pc.PcName}]  {pc.IP}");
    foreach (var s in pc.Stations)
        Console.WriteLine($"    {s.StationName}  recv:{s.LocalRecvPort}  send:{s.LocalSendPort}");
}

// ── Build hub and subscribe to events ───────────────────────────────────────
using var hub = new RecipeStationHub(stationConfigs);

foreach (var client in hub.Clients)
{
    var name = client.StationName;
    client.LogMessage             += (_, msg) => Console.WriteLine($"[{name}] {msg}");
    client.RecipeCopiedReceived   += (_, seq) => Console.WriteLine($"[{name}] Recipe copied  SEQ:{seq:X4}");
    client.RecipeDeletedReceived  += (_, seq) => Console.WriteLine($"[{name}] Recipe deleted SEQ:{seq:X4}");
    client.RecipeQuantityReceived += (_, e)   => Console.WriteLine($"[{name}] Recipe qty: {e.quantity} (type:{e.recipeType})");
    client.RecipeNameReceived     += (_, e)   => Console.WriteLine($"[{name}] Recipe [{e.index}]: \"{e.name}\" (type:{e.recipeType})");
    client.AllRecipeNamesReceived += (_, e)   =>
    {
        Console.WriteLine($"[{name}] All recipes (type:{e.recipeType}, count:{e.names.Count}):");
        for (int i = 0; i < e.names.Count; i++)
            Console.WriteLine($"[{name}]   [{i}] {e.names[i]}");
    };
    client.CommandFailedReceived  += (_, seq) => Console.WriteLine($"[{name}] Command failed SEQ:{seq:X4}");
}

// ── Go online ────────────────────────────────────────────────────────────────
hub.GoOnlineAll();
Console.WriteLine("\nAll stations online. Commands: ping <name> | copy <name> <src> <dst> | delete <name> <recipe> | count <name> [type] | getrecipe <name> <index> [type] | allrecipes <name> [type] | quit\n");

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
        {
            int recipeType = parts.Length >= 3 && int.TryParse(parts[2], out var rt) ? rt : 0;
            station.SendGetRecipeQuantity(recipeType);
            break;
        }
        case "getrecipe" when parts.Length >= 3:
        {
            if (!int.TryParse(parts[2], out int idx))
            {
                Console.WriteLine("Usage: getrecipe <stationName> <index> [type]");
                break;
            }
            int recipeType = parts.Length >= 4 && int.TryParse(parts[3], out var rt) ? rt : 0;
            station.SendGetRecipeName(idx, recipeType);
            break;
        }
        case "allrecipes":
        {
            int recipeType = parts.Length >= 3 && int.TryParse(parts[2], out var rt) ? rt : 0;
            station.SendGetAllRecipeNames(recipeType);
            break;
        }
        default:
            Console.WriteLine("Unknown command or missing arguments.");
            break;
    }
}

hub.GoOfflineAll();
Console.WriteLine("Disconnected.");
