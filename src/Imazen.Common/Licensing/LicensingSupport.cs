using System.Diagnostics;
using Imazen.Common.Instrumentation.Support;

namespace Imazen.Common.Licensing
{
    class RealClock : ILicenseClock
    {
        public long TicksPerSecond { get; } = Stopwatch.Frequency;

        public long GetTimestampTicks() => Stopwatch.GetTimestamp();

        public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public DateTimeOffset? GetBuildDate()
        {
            try
            {   
                // Fall back to the old attribute
                var type = GetType();
                return type.Assembly.GetFirstAttribute<Abstractions.AssemblyAttributes.BuildDateAttribute>()?.ValueDate ??
#pragma warning disable CS0618 // Type or member is obsolete
                       type.Assembly.GetFirstAttribute<BuildDateAttribute>()?.ValueDate;
#pragma warning restore CS0618 // Type or member is obsolete
                       
            } catch {
                return null;
            }
        }

        public DateTimeOffset? GetAssemblyWriteDate()
        {
            try {
                // .Location can throw, or be empty string (on AOT)
#pragma warning disable IL3000
                var path = GetType().Assembly.Location;
#pragma warning restore IL3000
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }
                return File.Exists(path)
                    ? new DateTimeOffset?(File.GetLastWriteTimeUtc(path))
                    : null;
            } catch {
                return null;
            }
        }
    }
}
