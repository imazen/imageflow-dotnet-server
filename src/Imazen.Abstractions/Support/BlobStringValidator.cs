namespace Imazen.Common.Extensibility.Support
{

    /// <summary>
    /// Validates strings for use as Azure and S3 blob keys and container names.
    /// </summary>
    public static class BlobStringValidator
    {

        /// <summary>
        /// Validates that the string is not null or empty, and must be less than 1024 bytes when encoded as UTF-8
        /// </summary>
        /// <param name="key"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal static bool ValidateKeyByteLength(string key, out string? error)
        {
            error = null;
            if (key == null || key.Length == 0)
            {
                error = "Blob keys cannot be null or empty";
                return false;
            }

            // Byte encoded as UTF-8 must be <= 1024 bytes
            if (System.Text.Encoding.UTF8.GetByteCount(key) > 1024)
            {
                error = "Blob keys cannot be longer than 1024 characters when encoded as UTF-8";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the key meets the recommended standards for both S3 and Azure blob keys.
        /// Unlike ValidateBlobKeySegmentStrict, leading and trailing slashes are prohibited.
        /// Should only contain letters, numbers, (),/, *, ', ., _, and -
        /// Should not contain //, start with /, ./, or ../, or end with /, \, /., ., or \.
        /// Should not be longer than 1024 bytes when encoded as UTF-8
        /// Should not be null or emtpy.
        /// </summary>
        /// Returns false if 
        /// <param name="key"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool ValidateBlobKeyStrict(string key, out string? error)
        {
            error = null;
            if (!ValidateBlobKeySegmentStrict(key, out error))
            {
                return false;
            }
            if (key.StartsWith("/") || key.EndsWith("/"))
            {
                error = "Key cannot start or end with /";
                return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if the key meets the recommended standards for both S3 and Azure path segments. 
        /// Full keys should not end or start with /, but this method permits it.
        /// Should not contain //, ./, start with /, ./, or ../, or end with /, \, /., ., or \.
        /// Should only contain letters, numbers, (),/, *, ', ., _, and -
        /// Should not be longer than 1024 bytes when encoded as UTF-8
        /// Should not be null or emtpy.
        /// </summary>
        /// Returns false if 
        /// <param name="key"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool ValidateBlobKeySegmentStrict(string key, out string? error)
        {
            error = null;

            if (!ValidateKeyByteLength(key, out error))
            {
                return false;
            }

            if (key.Contains("./"))
            {
                error = "Blob keys cannot contain ./";
                return false;
            }
            if (key.StartsWith("../"))
            {
                error = "Blob keys cannot start with ../";
                return false;
            }
            if (key.EndsWith("/."))
            {
                error = "Blob keys cannot end with /.";
                return false;
            }
            if (key.EndsWith("."))
            {
                error = "Blob keys cannot end with .";
                return false;
            }
            if (key.Contains("//"))
            {
                error = "Blob keys cannot contain //";
                return false;
            }

            //Also includes: Avoid blob names that end with a dot (.), a forward slash (/), a backslash (\), or a sequence or combination of the two.
            // No path segments should end with a dot (.).

            // only allow 0-9a-zA-Z()/'*._-! in keys
            if (!key.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '(' || c == ')' || c == '\'' || c == '/' || c == '*' || c == '.' || c == '_' || c == '-' || c == '!'))
            {
                error = "Blob keys should only contain a-z, A-Z, 0-9, (),/, *, ', ., _, !, and -";
                // also satisfies unicode limits https://learn.microsoft.com/en-us/rest/api/storageservices/Naming-and-Referencing-Containers--Blobs--and-Metadata
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that the string is a valid S3/Azure key prefix. 
        /// Same as ValidateBlobKeySegmentStrict, but leading slashes are prohibited.
        /// Can be null or empty, but must be less than 1024 bytes when encoded as UTF-8
        /// </summary>
        /// <param name="key"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal static bool ValidateBlobKeyPrefixStrict(string key, out string? error)
        {
            error = null;
            if (!ValidateBlobKeySegmentStrict(key, out error))
            {
                return false;
            }
            if (key.StartsWith("/"))
            {
                error = "Key prefix cannot start with /";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that the string is a valid Azure container name.
        /// Container names must be between 3 (min) and 63 (max) characters long.
        /// Container names can consist only of lowercase letters, numbers, and hyphens.
        /// Container names must begin and end with a letter.
        /// Container names must not contain two consecutive hyphens.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool ValidateAzureContainerName(string bucket, out string? error)
        {
            error = null;
            if (bucket == null || bucket.Length == 0)
            {
                error = "Azure container names cannot be null or empty";
                return false;
            }
            // must start or end with a-z
            if (!char.IsLetter(bucket[0]) && !char.IsLetter(bucket[bucket.Length - 1]))
            {
                error = "Azure container names must start or end with a letter";
                return false;
            }

            if (!bucket.All(c => (c >= 'a' && c <= 'z') || (c > '0' && c <= '9') ||  c == '-'))
            {
                error = "Azure container names can only contain [a-z0-9-]";
                return false;
            }

            if (bucket.Length < 3)
            {
                error = "Azure container names must be at least 3 characters long";
                return false;
            }

            if (bucket.Length > 63)
            {
                error = "Azure container names cannot be longer than 63 characters";
                return false;
            }
            
            if (bucket.Contains("--"))
            {
                // For Azure only
                error = "Azure container names cannot contain two dashes -- in a row";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that the string is a valid S3 bucket name
        /// </summary>
        /// <param name="bucket">
        /// Bucket names must be between 3 (min) and 63 (max) characters long.
        /// Bucket names can consist only of lowercase letters, numbers, dots (.), and hyphens (-).
        /// Bucket names must begin and end with a letter or number.
        /// Bucket names must not contain two adjacent periods.
        /// Bucket names must not be formatted as an IP address (for example, 192.168.5.4).
        /// Bucket names must not start with the prefix xn--.
        /// Bucket names must not start with the prefix sthree- and the prefix sthree-configurator.
        /// Bucket names must not end with the suffix -s3alias. This suffix is reserved for access point alias names. For more information, see Using a bucket-style alias for your S3 bucket access point.
        /// Bucket names must not end with the suffix --ol-s3. This suffix is reserved for Object Lambda Access Point alias names. For more information, see How to use a bucket-style alias for your S3 bucket Object Lambda Access Point.
        /// </param>
        /// <param name="error">The error message</param>
        /// <returns></returns>
        public static bool ValidateS3BucketName(string bucket, out string? error)
        {
            error = null;
            if (bucket == null || bucket.Length == 0)
            {
                error = "Container/bucket names cannot be null or empty";
                return false;
            }
            // Bucket names must not be formatted as an IP address (for example, 192.168.5.4).
            if (System.Net.IPAddress.TryParse(bucket, out _))
            {
                error = "Container/bucket names cannot be an IP address";
                return false;
            }
            // Bucket names can consist only of lowercase letters, numbers, dots (.), and hyphens (-).
            if (!bucket.All(c => (c >= 'a' && c <= 'z') || char.IsDigit(c) || c == '.' || c == '-'))
            {
                error = "Container/bucket names can only contain lowercase letters, numbers, dots, and hyphens";
                return false;
            }

            if (bucket.Length < 3)
            {
                error = "Container/bucket names must be at least 3 characters long";
                return false;
            }

            if (bucket.Length > 63)
            {
                error = "Container/bucket names cannot be longer than 63 characters";
                return false;
            }
            if (bucket.Contains(".."))
            {
                error = "Container/bucket names cannot contain two periods in a row";
                return false;
            }
            if (!char.IsLetterOrDigit(bucket[0]))
            {
                error = "Container/bucket names must start with a letter or number";
                return false;
            }

            if (!char.IsLetterOrDigit(bucket[bucket.Length - 1]))
            {
                error = "Container/bucket names must end with a letter or number";
                return false;
            }

            if (bucket.StartsWith("xn--"))
            {
                error = "Container/bucket names cannot start with xn--";
                return false;
            }

            if (bucket.EndsWith("-s3alias") || bucket.EndsWith("--ol-s3"))
            {
                error = "Bucket names cannot end with -s3alias or --ol-s3";
                return false;
            }

            if (bucket.StartsWith("sthree-") || bucket.StartsWith("sthree-configurator"))
            {
                error = "Bucket names cannot start with sthree- or sthree-configurator";
                return false;
            }

            return true;
        }
    }
}
