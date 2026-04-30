namespace RecipeStation.Client;

public interface IRecipeStationClient : IDisposable
{
    event EventHandler<string>                                               LogMessage;
    event EventHandler<ushort>                                               AcknowledgeReceived;
    event EventHandler<ushort>                                               RecipeCopiedReceived;
    event EventHandler<ushort>                                               RecipeDeletedReceived;
    event EventHandler<(ushort seq, int quantity, int recipeType)>           RecipeQuantityReceived;
    event EventHandler<(ushort seq, int index, string name, int recipeType)>   RecipeNameReceived;
    event EventHandler<(ushort seq, List<string> names, int recipeType)>      AllRecipeNamesReceived;
    event EventHandler<ushort>                                                CommandFailedReceived;

    string StationName { get; }
    bool   IsOnline    { get; }

    void GoOnline();
    void GoOffline();
    void SendPing();
    void SendCopyRecipe(string src, string dst);
    void SendDeleteRecipe(string name);
    void SendGetRecipeQuantity(int recipeType = 0);
    void SendGetRecipeName(int index, int recipeType = 0);
    void SendGetAllRecipeNames(int recipeType = 0);
}
