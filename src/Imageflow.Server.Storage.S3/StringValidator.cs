// utility functions to valid key and bucket names for usage on S3

// Path: src/Imageflow.Server.Storage.S3/StringValidator.cs
using System.Globalization;
using System.Linq;

namespace Imageflow.Server.Storage.S3
{
    internal static class StringValidator
    {
        /// <summary>
        /// Validates that the string is a valid S3 key. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal static bool ValidateKey(string key, out string error)
        {
            error = null;
            if (key == null || key.Length == 0)
            {
                error = "Key cannot be null or empty";
                return false;
            }

            // Byte encoded as UTF-8 must be <= 1024 bytes
            if (System.Text.Encoding.UTF8.GetByteCount(key) > 1024)
            {
                error = "Key cannot be longer than 1024 characters when encoded as UTF-8";
                return false;
            }

            return true;
        }

        // validate key is sane, ()'*._-!A-Za-z0-0
        internal static bool ValidateKeySane(string key, out string error)
        {
            error = null;

            if (!ValidateKey(key, out error))
            {
                return false;
            }

            if (key.StartsWith("/"))
            {
                error = "Key cannot start with /";
                return false;
            }
            if (key.StartsWith("./"))
            {
                error = "Key cannot start with ./";
                return false;
            }
            if (key.StartsWith("../"))
            {
                error = "Key cannot start with ../";
                return false;
            }
            if (key.EndsWith("."))
            {
                error = "Key cannot end with .";
                return false;
            }
            if (key.Contains("//"))
            {
                error = "Key cannot contain //";
                return false;
            }

            if (!key.ToList().All(c => char.IsLetterOrDigit(c) || c == '(' || c == ')' || c == '\'' || c=='/' || c == '.' || c == '_' || c == '-'))
            {
                error = "Key should only contain letters, numbers, (),/, *, ', ., _, and -";
                return false;
            }

            return true;
        }

        // validate key prefix is empty, null, or sand AND ends with /
        internal static bool ValidateKeyPrefix(string key, out string error)
        {
            error = null;
            if (key == null || key.Length == 0)
            {
                return true;
            }

            if (!ValidateKeySane(key, out error))
            {
                return false;
            }

            if (!key.EndsWith("/"))
            {
                error = "Key prefix must end with /";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the string is a valid S3 bucket name
        /// </summary>
        /// 
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
        internal static bool ValidateBucketName(string bucket, out string error)
        {
            error = null;
            if (bucket == null || bucket.Length == 0)
            {
                error = "Bucket name cannot be null or empty";
                return false;
            }

            // Bucket names must not be formatted as an IP address (for example, 192.168.5.4).
            if (System.Net.IPAddress.TryParse(bucket, out _))
            {
                error = "Bucket name cannot be an IP address";
                return false;
            }
            // Bucket names can consist only of lowercase letters, numbers, dots (.), and hyphens (-).
            if (!bucket.ToList().All(c => (c >= 'a' && c <= 'z') || char.IsDigit(c) || c == '.' || c == '-'))
            {
                error = "Bucket name can only contain lowercase letters, numbers, dots, and hyphens";
                return false;
            }

            if (bucket.Length < 3)
            {
                error = "Bucket name must be at least 3 characters long";
                return false;
            }

            if (bucket.Length > 63)
            {
                error = "Bucket name cannot be longer than 63 characters";
                return false;
            }
            if (bucket.Contains(".."))
            {
                error = "Bucket name cannot contain two periods in a row";
                return false;
            }

            if (!char.IsLetterOrDigit(bucket[0]))
            {
                error = "Bucket name must start with a letter or number";
                return false;
            }

            if (!char.IsLetterOrDigit(bucket[bucket.Length - 1]))
            {
                error = "Bucket name must end with a letter or number";
                return false;
            }

            if (bucket.StartsWith("xn--"))
            {
                error = "Bucket name cannot start with xn--";
                return false;
            }

            if (bucket.EndsWith("-s3alias") || bucket.EndsWith("--ol-s3"))
            {
                error = "Bucket name cannot end with -s3alias or --ol-s3";
                return false;
            }

            if (bucket.StartsWith("sthree-") || bucket.StartsWith("sthree-configurator"))
            {
                error = "Bucket name cannot start with sthree- or sthree-configurator";
                return false;
            }

            return true;
        }
    }
}
