namespace RecipeStation.Protocol.Ber;

[Flags]
public enum BerTagClasses
{
    Universal   = 0x00,
    Application = 0x40,
    Context     = 0x80,
    Private     = 0xC0
}
