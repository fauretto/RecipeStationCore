namespace RecipeStation.Protocol.Ber;

public class BerBoolean : BerTag
{
    public BerBoolean(bool value = false)
        : base(new BerTagIdentifier(BerTagClasses.Universal, BerTagConstruction.Primitive, BerUniversalTagNumbers.Boolean))
        => Value = value;

    public bool Value { get; set; }

    public override uint ContentLength => 1;

    protected override void SerializeContent(byte[] octets, uint start)
        => octets[start] = Value ? (byte)0xFF : (byte)0x00;

    protected override bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen)
    {
        if (contentLen != 1) return false;
        Value = octets[start] != 0x00;
        return true;
    }

    public override string ToString() => Value ? "(bool)[True]" : "(bool)[False]";
}
