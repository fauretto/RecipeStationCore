using System.Text;

namespace RecipeStation.Protocol.Ber;

public class BerVisibleString : BerTag
{
    private static readonly Encoding Ascii = Encoding.ASCII;

    public BerVisibleString(string value = "")
        : base(new BerTagIdentifier(BerTagClasses.Universal, BerTagConstruction.Primitive, BerUniversalTagNumbers.VisibleString))
        => Value = value;

    public string Value { get; set; }

    public override uint ContentLength => (uint)(Value?.Length ?? 0);

    protected override void SerializeContent(byte[] octets, uint start)
    {
        if (!string.IsNullOrEmpty(Value))
            Ascii.GetBytes(Value, 0, Value.Length, octets, (int)start);
    }

    protected override bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen)
    {
        Value = contentLen == 0 ? string.Empty : Ascii.GetString(octets, (int)start, (int)contentLen);
        return true;
    }

    public override string ToString() => $"(str)[{Value}]";
}
