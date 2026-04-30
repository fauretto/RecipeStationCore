namespace RecipeStation.Protocol.Ber;

public struct BerTagIdentifier
{
    public BerTagClasses    Class        { get; private set; }
    public BerTagConstruction Construction { get; private set; }
    public ushort           TagNumber    { get; private set; }

    public BerTagIdentifier(BerTagClasses cls, BerTagConstruction construction, ushort tagNumber = 0)
    {
        Class        = cls;
        Construction = construction;
        TagNumber    = tagNumber;
    }

    public void Initialize(BerTagClasses cls, BerTagConstruction construction, ushort tagNumber)
    {
        Class        = cls;
        Construction = construction;
        TagNumber    = tagNumber;
    }

    public ushort ComputeOctetCountForSerialization()
    {
        if (TagNumber < 30) return 1;
        ushort remaining = TagNumber;
        ushort count = 1;
        while (remaining > 0) { remaining >>= 7; count++; }
        return count;
    }

    public void Serialize(byte[] octets, uint start, out uint written)
    {
        ushort needed = ComputeOctetCountForSerialization();
        if (needed + start > octets.Length) throw new Exception("Insufficient octets for identifier");
        written = 1;
        if (needed == 1)
        {
            octets[start] = (byte)((byte)Class | (byte)Construction | (byte)TagNumber);
            return;
        }
        octets[start++] = (byte)((byte)Class | (byte)Construction | 0x1F);
        ushort rem = TagNumber;
        uint end = needed + start - 2;
        for (uint i = end; i >= start; i--)
        {
            byte b = (byte)(rem & 0x7F);
            b |= 0x80;
            rem >>= 7;
            octets[i] = b;
            written++;
        }
        octets[end] &= 0x7F;
    }

    public static BerTagIdentifier? Deserialize(byte[] octets, int length, uint start, out uint read)
    {
        if (octets == null || length - start < 1) { read = 0; return null; }
        read = 1;
        var cls   = (BerTagClasses)   (octets[start] & 0xC0);
        var cons  = (BerTagConstruction)(octets[start] & 0x20);
        ushort num = (ushort)(octets[start] & 0x1F);
        if (num != 0x1F) return new BerTagIdentifier(cls, cons, num);

        num = 0;
        ushort iter = 0;
        byte cur;
        do
        {
            if (iter++ == 2 || length - start < 1) { read = 0; return null; }
            cur = octets[++start];
            num = (ushort)((num << 7) | (ushort)(cur & 0x7F));
            read++;
        } while ((cur & 0x80) != 0);
        return new BerTagIdentifier(cls, cons, num);
    }

    public override string ToString() => Construction switch
    {
        BerTagConstruction.Constructed => Class switch
        {
            BerTagClasses.Application => "AC",
            BerTagClasses.Context     => "CC",
            BerTagClasses.Private     => "PC",
            _                         => "UC"
        },
        _ => Class switch
        {
            BerTagClasses.Application => "AP",
            BerTagClasses.Context     => "CP",
            BerTagClasses.Private     => "PP",
            _                         => "UP"
        }
    };
}
