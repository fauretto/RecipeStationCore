namespace RecipeStation.Protocol.Ber;

public class BerInteger : BerTag
{
    public BerInteger(int value = 0)
        : base(new BerTagIdentifier(BerTagClasses.Universal, BerTagConstruction.Primitive, BerUniversalTagNumbers.Integer))
        => Value = value;

    public int Value { get; set; }

    public override uint ContentLength
    {
        get
        {
            uint rem = (uint)(Value < 0 ? -Value : Value);
            rem <<= 1;
            ushort n = 1;
            while (rem > 0xFF) { rem >>= 8; n++; }
            return n;
        }
    }

    protected override void SerializeContent(byte[] octets, uint start)
    {
        uint len = ContentLength;
        uint rem = (uint)Value;
        for (uint i = len + start - 1; i >= start; i--)
        {
            octets[i] = (byte)(rem & 0xFF);
            rem >>= 8;
        }
    }

    protected override bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen)
    {
        if (contentLen == 0) return false;
        uint rem = (octets[start] & 0x80) == 0x80 ? 0xFFFFFFFF : 0x00000000;
        for (uint i = 0; i < contentLen; i++)
            rem = (rem << 8) | octets[start + i];
        Value = (int)rem;
        return true;
    }

    public override string ToString() => $"(int)[{Value}]";
}
