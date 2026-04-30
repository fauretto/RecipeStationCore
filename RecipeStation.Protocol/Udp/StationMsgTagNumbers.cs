namespace RecipeStation.Protocol.Udp;

public static class StationMsgTagNumbers
{
    // Commands -> station
    public const ushort GetRecipeQuantity = 11;
    public const ushort GetRecipeName     = 12;
    public const ushort CopyRecipe        = 57;
    public const ushort DeleteRecipe      = 58;

    // Events ← station
    public const ushort SendRecipeQuantity = 35;
    public const ushort SendRecipeName     = 36;
    public const ushort RecipeCopied       = 61;
    public const ushort RecipeDeleted      = 62;

    // Variables
    public const ushort RecipeTypeTag = 3;   // 0 = production, 1 = calibration
    public const ushort RecipeName    = 67;
}
