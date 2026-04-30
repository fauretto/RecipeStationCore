using Microsoft.Extensions.Configuration;
using RecipeStation.Client;
using RecipeStation.Simulator;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var pcConfigs = config.GetSection("PCs").Get<List<PcConfig>>()
    ?? throw new InvalidOperationException("No PCs found in appsettings.json");

var simulators = pcConfigs
    .SelectMany(pc => pc.Stations.Select(s => new StationSimulator(s.ToRecipeStationConfig(pc.IP))))
    .ToList();

Console.WriteLine($"Starting {simulators.Count} station simulator(s)...\n");
simulators.ForEach(s => s.Start());

Console.WriteLine("\nAll simulators running. Press Enter to stop.");
Console.ReadLine();

simulators.ForEach(s => s.Dispose());
Console.WriteLine("Stopped.");
