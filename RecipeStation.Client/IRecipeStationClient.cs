namespace RecipeStation.Client;

public interface IRecipeStationClient : IDisposable
{
    event EventHandler<string>                    LogMessage;
    event EventHandler<ushort>                    AcknowledgeReceived;
    event EventHandler<ushort>                    RecipeCopiedReceived;
    event EventHandler<ushort>                    RecipeDeletedReceived;
    event EventHandler<(ushort seq, int quantity)> RecipeQuantityReceived;
    event EventHandler<ushort>                    CommandFailedReceived;

    string StationName { get; }
    bool   IsOnline    { get; }

    void GoOnline();
    void GoOffline();
    void SendPing();
    void SendCopyRecipe(string src, string dst);
    void SendDeleteRecipe(string name);
    void SendGetRecipeQuantity();
}
