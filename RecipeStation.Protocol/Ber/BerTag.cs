namespace RecipeStation.Protocol.Ber;

public abstract class BerTag
{
    private BerTagIdentifier _identifier;

    protected BerTag(BerTagIdentifier identifier) => _identifier = identifier;

    public BerTagIdentifier Identifier => _identifier;

    protected void Initialize(BerTagClasses cls, BerTagConstruction cons, ushort num)
        => _identifier.Initialize(cls, cons, num);

    private ushort ComputeContentLengthOctetCount()
    {
        uint rem = ContentLength;
        if (rem <= 0x7F) return 1;
        ushort n = 2;
        while (rem > 0xFF) { rem >>= 8; n++; }
        return n;
    }

    public uint ComputeOctetCountForSerialization()
    {
        return ContentLength
             + _identifier.ComputeOctetCountForSerialization()
             + ComputeContentLengthOctetCount();
    }

    public void Serialize(byte[] octets, uint start, out uint written)
    {
        uint contentLen = ContentLength;
        ushort idOctets  = _identifier.ComputeOctetCountForSerialization();
        ushort lenOctets = ComputeContentLengthOctetCount();

        if (contentLen + idOctets + lenOctets + start > octets.Length)
            throw new Exception("Insufficient octets for writing");

        _identifier.Serialize(octets, start, out written);
        start += written;

        if (lenOctets > 1)
        {
            octets[start] = (byte)((lenOctets - 1) | 0x80);
            start++;
            lenOctets--;
            written++;
        }

        uint rem = contentLen;
        for (uint i = lenOctets + start - 1; i >= start; i--)
        {
            octets[i] = (byte)(rem & 0xFF);
            rem >>= 8;
        }
        start   += lenOctets;
        written += lenOctets;
        SerializeContent(octets, start);
        written += contentLen;
    }

    public byte[] Serialize()
    {
        var buf = new byte[ComputeOctetCountForSerialization()];
        Serialize(buf, 0, out _);
        return buf;
    }

    public static BerTag? Deserialize(byte[] octets, int length, uint start, out uint read)
    {
        var id = BerTagIdentifier.Deserialize(octets, length, start, out uint idRead);
        if (id == null) { read = 0; return null; }
        start += idRead;
        if (start >= length) { read = 0; return null; }

        uint contentLen;
        uint lenRead;
        if ((octets[start] & 0x80) == 0x00)
        {
            contentLen = (uint)(octets[start] & 0x7F);
            lenRead = 1;
            start++;
        }
        else
        {
            byte lenBytes = (byte)(octets[start] & 0x7F);
            start++;
            if (start + lenBytes > length) { read = 0; return null; }
            contentLen = 0;
            for (byte i = 0; i < lenBytes; i++)
                contentLen = (contentLen << 8) | octets[i + start];
            start += lenBytes;
            lenRead = (uint)(lenBytes + 1);
        }

        if (start + contentLen > octets.Length) { read = 0; return null; }

        BerTag? tag = null;
        if (id.Value.Class == BerTagClasses.Universal && id.Value.Construction == BerTagConstruction.Primitive)
        {
            tag = id.Value.TagNumber switch
            {
                BerUniversalTagNumbers.Boolean       => DeserializeLeaf(new BerBoolean(), octets, length, start, contentLen),
                BerUniversalTagNumbers.Integer       => DeserializeLeaf(new BerInteger(), octets, length, start, contentLen),
                BerUniversalTagNumbers.VisibleString => DeserializeLeaf(new BerVisibleString(), octets, length, start, contentLen),
                _                                    => null
            };
        }
        else if (id.Value.Class == BerTagClasses.Application)
        {
            if (id.Value.Construction == BerTagConstruction.Constructed)
            {
                var c = new BerAppConstructed(id.Value.TagNumber);
                tag = c.DeserializeContent(octets, length, start, contentLen) ? c : null;
            }
            else
            {
                var p = new BerAppPrimitive(id.Value.TagNumber);
                tag = p.DeserializeContent(octets, length, start, contentLen) ? p : null;
            }
        }

        if (tag == null) { read = 0; return null; }
        read = idRead + lenRead + contentLen;
        return tag;
    }

    private static BerTag? DeserializeLeaf(BerTag tag, byte[] octets, int length, uint start, uint contentLen)
        => tag.DeserializeContent(octets, length, start, contentLen) ? tag : null;

    public abstract uint ContentLength { get; }
    protected abstract void SerializeContent(byte[] octets, uint start);
    protected abstract bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen);
}
