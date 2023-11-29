namespace Imazen.Abstractions.Internal;

internal struct Fnv1AHash
{
    private const ulong FnvPrime = 0x00000100000001B3;
    private const ulong FnvOffsetBasis = 0xCBF29CE484222325;
    private ulong hash;
    public ulong CurrentHash => hash;

    public static Fnv1AHash Create()
    {
        return new Fnv1AHash(){
            hash = FnvOffsetBasis
        };
    }

    private void HashInternal(ReadOnlySpan<byte> array)
    {
        foreach (var t in array)
        {
            unchecked
            {
                hash ^= t;
                hash *= FnvPrime;
            }
        }
    }
    private void HashInternal(ReadOnlySpan<char> array)
    {
        foreach (var t in array)
        {
            unchecked
            {
                hash ^= (byte)(t & 0xFF);
                hash *= FnvPrime;
                hash ^= (byte)((t >> 8) & 0xFF);
                hash *= FnvPrime;
            }
        }
    }
    public void Add(string? s)
    {
        if (s == null) return;
        HashInternal(s.AsSpan());
    }
    public void Add(ReadOnlySpan<char> s)
    {
        HashInternal(s);
    }

    public void Add(byte[] array)
    {
        HashInternal(array);
    }
    public void Add(int i)
    {
        unchecked
        {
            byte b1 = (byte) (i & 0xFF);
            byte b2 = (byte) ((i >> 8) & 0xFF);
            byte b3 = (byte) ((i >> 16) & 0xFF);
            byte b4 = (byte) ((i >> 24) & 0xFF);
            hash ^= b1;
            hash *= FnvPrime;
            hash ^= b2;
            hash *= FnvPrime;
            hash ^= b3;
            hash *= FnvPrime;
            hash ^= b4;
        }
    }
    public void Add(long val)
    {
        Add((int)(val & 0xFFFFFFFF));
        Add((int) (val >> 32));
    }
        
}