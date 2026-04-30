namespace RecipeStation.Client;

/// <summary>
/// Manages a named set of <see cref="IRecipeStationClient"/> instances built from configuration.
/// </summary>
public sealed class RecipeStationHub : IDisposable
{
    private readonly Dictionary<string, IRecipeStationClient> _clients;
    private bool _disposed;

    public RecipeStationHub(IEnumerable<RecipeStationConfig> configs)
    {
        _clients = configs.ToDictionary(
            c => c.StationName,
            c => (IRecipeStationClient)new RecipeStationClient(c));
    }

    public IRecipeStationClient this[string name] => _clients[name];

    public IReadOnlyCollection<IRecipeStationClient> Clients
        => _clients.Values.ToList().AsReadOnly();

    public IReadOnlyCollection<string> StationNames
        => _clients.Keys.ToList().AsReadOnly();

    public void GoOnlineAll()  { foreach (var c in _clients.Values) c.GoOnline();  }
    public void GoOfflineAll() { foreach (var c in _clients.Values) c.GoOffline(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var c in _clients.Values) c.Dispose();
    }
}
