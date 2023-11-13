using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Imazen.Common.BlobStorage
{
    /// <summary>
    /// Enforces the following constraints on metadata:
    /// - Keys must start with an alphanumeric character and contain only alphanumeric characters, dashes, underscores, and periods.
    /// - Keys must be no longer than 128 bytes.
    /// - Values must be no longer than 256 bytes.
    /// - Total metadata size must be no longer than 2048 bytes.
    /// - Values must contain only ASCII characters.
    /// </summary>
    internal class BlobMetadata 
    {
        private const int MaxKeyLengthBytes = 128;
        private const int MaxValueLengthBytes = 256;
        private const int MaxTotalSizeBytes = 2048; // 2 KB
        private Dictionary<string, string> metadata;
        private int currentTotalSizeBytes;

        private static readonly Regex ValidKeyCharacters = new Regex("^[a-zA-Z0-9][a-zA-Z0-9-_\\.]*$");

        public BlobMetadata()
        {
            metadata = new Dictionary<string, string>();
            currentTotalSizeBytes = 0;
        }

        public void Set(string key, string value)
        {
            ValidateKey(key);
            ValidateValue(value);
            ValidateTotalSize(Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value));

            metadata[key] = value;
            currentTotalSizeBytes += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
        }

        public string Get(string key)
        {
            return metadata.ContainsKey(key) ? metadata[key] : null;
        }

        private void ValidateKey(string key)
        {
            if (!ValidKeyCharacters.IsMatch(key))
            {
                throw new BlobMetadataException("Invalid key. Must start with an alphanumeric character and contain only alphanumeric characters, dashes, underscores, and periods.");
            }
            ValidateSize(key, MaxKeyLengthBytes, "Key");
        }

        private void ValidateValue(string value)
        {
            if (!IsAscii(value))
            {
                throw new BlobMetadataException("Value contains non-ASCII characters.");
            }
            ValidateSize(value, MaxValueLengthBytes, "Value");
        }

        private void ValidateTotalSize(int additionalSize)
        {
            if (currentTotalSizeBytes + additionalSize > MaxTotalSizeBytes)
            {
                throw new BlobMetadataException($"Total metadata size exceeds {MaxTotalSizeBytes} bytes.");
            }
        }

        private bool IsAscii(string value)
        {
            return Encoding.UTF8.GetByteCount(value) == value.Length;
        }

        private void ValidateSize(string str, int maxSize, string type)
        {
            int byteCount = Encoding.UTF8.GetByteCount(str);
            if (byteCount > maxSize)
            {
                throw new BlobMetadataException($"{type} length exceeds {maxSize} bytes.");
            }
        }

            public IReadOnlyCollection<KeyValuePair<string, string>> GetAll()
            {
                throw new NotImplementedException();
            }
        
    }
}
