namespace Imazen.Routing.Helpers;

public static class HexHelpers
{
    public static string ToHexLowercase(this byte[] buffer) => ToHexLowercase(buffer.AsSpan());
    public static string ToHexLowercase(this ReadOnlySpan<byte> buffer)
    {
        Span<char> hexBuffer = buffer.Length > 1024 ? new char[buffer.Length * 2] : stackalloc char[buffer.Length * 2];
        
        for(var i = 0; i < buffer.Length; i++)
        {
            var b = buffer[i];
            var nibbleA = b >> 4;
            var nibbleB = b & 0x0F;
            hexBuffer[i * 2] = ToCharLower(nibbleA);
            hexBuffer[i * 2 + 1] = ToCharLower(nibbleB);
        }
        return hexBuffer.ToString();
    }
    public static string ToHexLowercaseWith(this ReadOnlySpan<byte> buffer, ReadOnlySpan<char> prefix, ReadOnlySpan<char> suffix)
    {
        var prefixLength = prefix.Length;
        var bufferOutputLength = buffer.Length * 2;
        var requiredChars = prefixLength + bufferOutputLength + suffix.Length;
        Span<char> hexBuffer = requiredChars > 1024 ? new char[requiredChars] : stackalloc char[requiredChars];
        
        prefix.CopyTo(hexBuffer);
        for(var i = 0; i < buffer.Length; i++)
        {
            var b = buffer[i];
            var nibbleA = b >> 4;
            var nibbleB = b & 0x0F;
            hexBuffer[prefixLength + (i * 2)] = ToCharLower(nibbleA);
            hexBuffer[prefixLength + (i * 2) + 1] = ToCharLower(nibbleB);
        }
        suffix.CopyTo(hexBuffer[(prefixLength + bufferOutputLength)..]);
        return hexBuffer.ToString();
    }
        
        
    
    private static char ToCharLower(int value)
    {
        value &= 0xF;
        value += '0';
        if (value > '9')
        {
            value += ('a' - ('9' + 1));
        }
        return (char)value;
    }
}