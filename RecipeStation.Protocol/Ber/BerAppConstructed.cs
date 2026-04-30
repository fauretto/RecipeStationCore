namespace RecipeStation.Protocol.Ber;

public class BerAppConstructed : BerConstructed
{
    public BerAppConstructed() : base(BerTagClasses.Application) { }
    public BerAppConstructed(ushort tagNumber) : base(BerTagClasses.Application, tagNumber) { }
    public void Initialize(ushort tagNumber) => base.Initialize(BerTagClasses.Application, tagNumber);
}
