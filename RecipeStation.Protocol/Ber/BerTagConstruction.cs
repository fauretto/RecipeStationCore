namespace RecipeStation.Protocol.Ber;

[Flags]
public enum BerTagConstruction
{
    Primitive   = 0x00,
    Constructed = 0x20
}
