namespace RecipeStation.Protocol.Ber;

public class BerAppPrimitive : BerTag
{
    public BerAppPrimitive() : this(default) { }
    public BerAppPrimitive(ushort tagNumber)
        : base(new BerTagIdentifier(BerTagClasses.Application, BerTagConstruction.Primitive, tagNumber)) { }

    public void Initialize(ushort tagNumber)
        => base.Initialize(BerTagClasses.Application, BerTagConstruction.Primitive, tagNumber);

    public override uint ContentLength => 0;
    protected override void SerializeContent(byte[] octets, uint start) { }
    protected override bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen)
        => contentLen == 0;

    public override string ToString() => $"{Identifier} []";
}
