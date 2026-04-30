namespace RecipeStation.Protocol.Ber;

public class BerConstructed : BerTag, IList<BerTag>
{
    private readonly List<BerTag> _children = [];

    protected BerConstructed(BerTagClasses cls, ushort tagNumber)
        : base(new BerTagIdentifier(cls, BerTagConstruction.Constructed, tagNumber)) { }

    protected BerConstructed(BerTagClasses cls)
        : base(new BerTagIdentifier(cls, BerTagConstruction.Constructed)) { }

    public void Initialize(BerTagClasses cls, ushort tagNumber)
    {
        base.Initialize(cls, BerTagConstruction.Constructed, tagNumber);
        _children.Clear();
    }

    public override uint ContentLength
    {
        get
        {
            uint len = 0;
            foreach (var c in _children) len += c.ComputeOctetCountForSerialization();
            return len;
        }
    }

    protected override void SerializeContent(byte[] octets, uint start)
    {
        foreach (var c in _children)
        {
            c.Serialize(octets, start, out uint w);
            start += w;
        }
    }

    protected override bool DeserializeContent(byte[] octets, int length, uint start, uint contentLen)
    {
        _children.Clear();
        uint read = 0;
        while (read < contentLen)
        {
            var tag = BerTag.Deserialize(octets, length, start + read, out uint r);
            if (tag == null) { _children.Clear(); return false; }
            _children.Add(tag);
            read += r;
        }
        return read == contentLen;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Identifier).Append(" <");
        foreach (var c in _children) sb.Append(' ').Append(c);
        sb.Append(" >");
        return sb.ToString();
    }

    // IList<BerTag>
    public BerTag this[int i]   { get => _children[i]; set => _children[i] = value; }
    public int  Count           => _children.Count;
    public bool IsReadOnly      => false;
    public void Add(BerTag item)             => _children.Add(item);
    public void Clear()                      => _children.Clear();
    public bool Contains(BerTag item)        => _children.Contains(item);
    public void CopyTo(BerTag[] arr, int i)  => _children.CopyTo(arr, i);
    public int  IndexOf(BerTag item)         => _children.IndexOf(item);
    public void Insert(int i, BerTag item)   => _children.Insert(i, item);
    public bool Remove(BerTag item)          => _children.Remove(item);
    public void RemoveAt(int i)              => _children.RemoveAt(i);
    public IEnumerator<BerTag> GetEnumerator()                     => _children.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _children.GetEnumerator();
}
