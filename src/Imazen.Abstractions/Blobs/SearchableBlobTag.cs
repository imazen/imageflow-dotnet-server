using Imazen.Common.Extensibility.Support;

namespace Imazen.Abstractions.Blobs;

// TODO: would be nice to keep this in u8 to halve memory usage.


/// <summary>
/// For indexed tags (s3 tags are not, and don't count)
/// These are also stored as metadata. They are not subject to the same encryption as blob contents
/// and should not be used to store PHI or sensitive data. Remember to use seeded fingerprints
/// of paths if you're trying to make it possible to query variants by path without making the path
/// visible as metadata.
/// These should be limited to the subset supported by all providers as both tags and metadata.
/// </summary>
public readonly record struct SearchableBlobTag : IEstimateAllocatedBytesRecursive
{
    private SearchableBlobTag(string key, string value)
    {
        Key = key;
        Value = value;
    }
    public string Key { get; init; }
    public string Value { get; init; }
    
    public KeyValuePair<string, string> ToKeyValuePair() => new(Key, Value);
    
    public int EstimateAllocatedBytesRecursive => 
        Key.EstimateMemorySize(true) + Value.EstimateMemorySize(true) + 8;

    private static bool ValidateChars(string keyOrValue)
    {
        // In S3/Azure storage tags, Allowed chars are: a-z, A-Z, 0-9, space, +, -, =, ., _, :, /
        //TODO In metadata, rules vary. Azure metadata must be valid C# identifier
        // Most must be valid HTTP 1.1 headers and the key may or may not be case insensitive.
        // keys can be ^[a-zA-Z0-9][a-zA-Z0-9-_\\.]*$
        
        foreach (var c in keyOrValue)
        {
            switch (c)
            {
                case >= 'a' and <= 'z':
                case >= 'A' and <= 'Z':
                case >= '0' and <= '9':
                case ' ':
                case '+':
                case '-':
                case '=':
                case '.':
                case '_':
                case ':':
                case '/':
                // case '@': (Azure only)
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }
    
    public bool IsStrictlyValid()
    {
        return Key.Length is >= 1 and <= 128 &&
               Value.Length <= 256 &&
               ValidateChars(Key) &&
               ValidateChars(Value);
    }
    
    public static SearchableBlobTag CreateAndValidateStrict(string key, string value)
    {
        if (key.Length is < 1 or > 128)
            throw new ArgumentException("Tag key must be between 1 and 128 characters", nameof(key));
        if (value.Length > 256)
            throw new ArgumentException("Tag value must be between 0 and 256 characters", nameof(value));
        if (!ValidateChars(key))
            throw new ArgumentException("Tag key contains invalid characters. Allowed chars are: a-z, A-Z, 0-9, space, +, -, =, ., _, :, /", nameof(key));
        if (!ValidateChars(value))
            throw new ArgumentException("Tag value contains invalid characters. Allowed chars are: a-z, A-Z, 0-9, space, +, -, =, ., _, :, /", nameof(value));
        return new SearchableBlobTag(key, value);
    }

    

    public static SearchableBlobTag CreateUnvalidated(string key, string value)
    {
        return new SearchableBlobTag(key, value);
    }

  
}

    
    // Amazon S3
    // You can add tags to new objects when you upload them, or you can add them to existing objects.
    //
    // You can associate up to 10 tags with an object. Tags that are associated with an object must have unique tag keys.
    //
    //     A tag key can be up to 128 Unicode characters in length, and tag values can be up to 256 Unicode characters in length. Amazon S3 object tags are internally represented in UTF-16. Note that in UTF-16, characters consume either 1 or 2 character positions.
    //
    //     The key and values are case sensitive.
    // AWS services allow: letters (a-z, A-Z), numbers (0-9), and spaces representable in UTF-8, and the following characters: + - = . _ : / @.
    // Azure
    // The following limits apply to blob index tags:
    //
    // Each blob can have up to 10 blob index tags
    //
    //     Tag keys must be between one and 128 characters.
    //
    //     Tag values must be between zero and 256 characters.
    //
    //     Tag keys and values are case-sensitive.
    //
    //     Tag keys and values only support string data types. Any numbers, dates, times, or special characters are saved as strings.
    //
    //     If versioning is enabled, index tags are applied to a specific version of blob. If you set index tags on the current version, and a new version is created, then the tag won't be associated with the new version. The tag will be associated only with the previous version.
    //
    //     Tag keys and values must adhere to the following naming rules:
    //
    // Alphanumeric characters:
    //
    // a through z (lowercase letters)
    //
    // A through Z (uppercase letters)
    //
    //     0 through 9 (numbers)
    //
    //     Valid special characters: space, plus, minus, period, colon, equals, underscore, forward slash ( +-.:=_/
    