using RecipeStation.Protocol.Ber;

namespace RecipeStation.Protocol.Udp;

/// <summary>
/// A framed UDP message: 2-byte CRC | 2-byte sequence number | BER tag payload.
/// </summary>
public class UdpMsg
{
    public ushort SequenceNumber { get; }
    public BerTag Tag            { get; }

    public UdpMsg(ushort sequenceNumber, BerTag tag)
    {
        SequenceNumber = sequenceNumber;
        Tag            = tag;
    }

    public byte[] Serialize()
    {
        var buf = new byte[4 + Tag.ComputeOctetCountForSerialization()];
        Tag.Serialize(buf, 4, out uint written);
        if (written != buf.Length - 4) throw new Exception("BerTag serialization length mismatch");

        buf[2] = (byte)(SequenceNumber >> 8);
        buf[3] = (byte)(SequenceNumber & 0xFF);

        ushort crc = ComputeCrc(buf, 2, buf.Length);
        buf[0] = (byte)(crc >> 8);
        buf[1] = (byte)(crc & 0xFF);
        return buf;
    }

    public static UdpMsg? Deserialize(byte[] octets, int length)
    {
        if (length < 5) return null;

        ushort crc = (ushort)((octets[0] << 8) | octets[1]);
        if (crc != ComputeCrc(octets, 2, length)) return null;

        ushort seq = (ushort)((octets[2] << 8) | octets[3]);
        var tag = BerTag.Deserialize(octets, length, 4, out uint read);
        if (tag == null || read != length - 4) return null;

        return new UdpMsg(seq, tag);
    }

    private static ushort ComputeCrc(byte[] data, int start, int end)
    {
        const ushort Poly = 0xA001;
        ushort crc = 0;
        for (int i = start; i < end; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ Poly) : (ushort)(crc >> 1);
        }
        return crc;
    }

    public override string ToString() => $"SEQ:{SequenceNumber:X4} TAG:{Tag}";
}
