using System;

namespace Imazen.Common.ExtensionMethods
{
    public static class DateTimeExtensions
    {
        public static long ToUnixTimeUtc(this DateTime dateTime)
        {
            return (long)(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static DateTime UnixTimeUtcIntoDateTime(this long unixValue)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixValue);
        }
    }
}