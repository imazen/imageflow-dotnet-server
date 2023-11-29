using System.Collections.Concurrent;
using System.Diagnostics;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Instrumentation.Support.PercentileSinks;
using Imazen.Common.Instrumentation.Support.RateTracking;
using Imazen.Common.Issues;


namespace Imazen.Common.Instrumentation
{

    // https://github.com/jawa-the-hutt/lz-string-csharp/blob/master/src/LZString.cs
    // https://github.com/maiwald/lz-string-ruby
    internal class GlobalPerf
    {
        readonly IssueSink sink = new("GlobalPerf");

        public static GlobalPerf Singleton { get; } = new();

        Lazy<BasicProcessInfo> Process { get; } = new();
        Lazy<HardwareInfo> Hardware { get; }

        private static ICollection<IInfoProvider>? _lastInfoProviders;
            
        NamedInterval[] Intervals { get; } =
        [
            new() { Unit="second", Name="Per Second", TicksDuration =  Stopwatch.Frequency},
            new() { Unit="minute", Name="Per Minute", TicksDuration =  Stopwatch.Frequency * 60},
            new() { Unit="15_mins", Name="Per 15 Minutes", TicksDuration =  Stopwatch.Frequency * 60 * 15},
            new() { Unit="hour", Name="Per Hour", TicksDuration =  Stopwatch.Frequency * 60 * 60}
        ];

        readonly ConcurrentDictionary<string, MultiIntervalStats> rates = new();

        readonly MultiIntervalStats blobReadEvents;
        readonly MultiIntervalStats blobReadBytes;
        readonly MultiIntervalStats jobs;
        readonly MultiIntervalStats decodedPixels;
        readonly MultiIntervalStats encodedPixels;

        readonly ConcurrentDictionary<string, IPercentileProviderSink> percentiles = new();

        readonly IPercentileProviderSink jobTimes;
        readonly IPercentileProviderSink decodeTimes;
        readonly IPercentileProviderSink encodeTimes;
        readonly IPercentileProviderSink jobOtherTime;
        readonly IPercentileProviderSink blobReadTimes;
        readonly IPercentileProviderSink collectInfoTimes;

        readonly DictionaryCounter<string> counters = new("counter_update_failed");

        IEnumerable<int> Percentiles { get; }  = new[] { 5, 25, 50, 75, 95, 100 };


        readonly IPercentileProviderSink sourceWidths;
        readonly IPercentileProviderSink sourceHeights;
        readonly IPercentileProviderSink outputWidths;
        readonly IPercentileProviderSink outputHeights;
        readonly IPercentileProviderSink sourceMegapixels;
        readonly IPercentileProviderSink outputMegapixels;
        readonly IPercentileProviderSink scalingRatios;
        readonly IPercentileProviderSink sourceAspectRatios;
        readonly IPercentileProviderSink outputAspectRatios;

        GlobalPerf()
        {
            Hardware = new Lazy<HardwareInfo>(() => new HardwareInfo(sink));
            blobReadBytes = rates.GetOrAdd("blob_read_bytes", new MultiIntervalStats(Intervals));
            blobReadEvents = rates.GetOrAdd("blob_reads", new MultiIntervalStats(Intervals));
            jobs = rates.GetOrAdd("jobs_completed", new MultiIntervalStats(Intervals));
            decodedPixels = rates.GetOrAdd("decoded_pixels", new MultiIntervalStats(Intervals));
            encodedPixels = rates.GetOrAdd("encoded_pixels", new MultiIntervalStats(Intervals));

            jobTimes = percentiles.GetOrAdd("job_times", new TimingsSink());
            decodeTimes = percentiles.GetOrAdd("decode_times", new TimingsSink());
            encodeTimes = percentiles.GetOrAdd("encode_times", new TimingsSink());
            jobOtherTime = percentiles.GetOrAdd("job_other_time", new TimingsSink());
            blobReadTimes = percentiles.GetOrAdd("blob_read_times", new TimingsSink());
            collectInfoTimes = percentiles.GetOrAdd("collect_info_times", new TimingsSink());

            sourceMegapixels = percentiles.GetOrAdd("source_pixels", new PixelCountSink());
            outputMegapixels = percentiles.GetOrAdd("output_pixels", new PixelCountSink());
            sourceWidths = percentiles.GetOrAdd("source_width", new ResolutionsSink());
            sourceHeights = percentiles.GetOrAdd("source_height", new ResolutionsSink());
            outputWidths = percentiles.GetOrAdd("output_width", new ResolutionsSink());
            outputHeights = percentiles.GetOrAdd("output_height", new ResolutionsSink());

            scalingRatios = percentiles.GetOrAdd("scaling_ratio", new FlatSink(1000));
            sourceAspectRatios = percentiles.GetOrAdd("source_aspect_ratio", new FlatSink(1000));
            outputAspectRatios = percentiles.GetOrAdd("output_aspect_ratio", new FlatSink(1000));

        }


        public void SetInfoProviders(ICollection<IInfoProvider> providers)
        {
            _lastInfoProviders = providers;
        }

        public void JobComplete(IImageJobInstrumentation job)
        {

            IncrementCounter("image_jobs");
            
            var timestamp = Stopwatch.GetTimestamp();
            var sW = job.SourceWidth.GetValueOrDefault(0);
            var sH = job.SourceHeight.GetValueOrDefault(0);
            var fW = job.FinalWidth.GetValueOrDefault(0);
            var fH = job.FinalHeight.GetValueOrDefault(0);


            if (job is { SourceWidth: not null, SourceHeight: not null })
            {
                var prefix = "source_multiple_";
                if (sW % 4 == 0 && sH % 4 == 0)
                {
                    counters.Increment(prefix + "4x4");
                }

                if (sW % 8 == 0 && sH % 8 == 0)
                {
                    counters.Increment(prefix + "8x8");
                }

                if (sW % 8 == 0)
                {
                    counters.Increment(prefix + "8x");
                }

                if (sH % 8 == 0)
                {
                    counters.Increment(prefix + "x8");
                }

                if (sW % 16 == 0 && sH % 16 == 0)
                {
                    counters.Increment(prefix + "16x16");
                }

            }



            //(builder.SettingsModifier as PipelineConfig).GetImageBuilder

            var readPixels = job.SourceWidth.GetValueOrDefault(0) * job.SourceHeight.GetValueOrDefault(0);
            var wrotePixels = job.FinalWidth.GetValueOrDefault(0) * job.FinalHeight.GetValueOrDefault(0);

            if (readPixels > 0)
            {
                sourceMegapixels.Report(readPixels);

                sourceWidths.Report(sW);
                sourceHeights.Report(sH);

                sourceAspectRatios.Report(sW * 100 / sH);
            }

            if (wrotePixels > 0)
            {
                outputMegapixels.Report(wrotePixels);


                outputWidths.Report(fW);
                outputHeights.Report(fH);
                outputAspectRatios.Report(fW * 100 / fH);
            }

            if (readPixels > 0 && wrotePixels > 0)
            {
                scalingRatios.Report(sW * 100 / fW);
                scalingRatios.Report(sH * 100 / fH);
            }

            jobs.Record(timestamp, 1);
            decodedPixels.Record(timestamp, readPixels);
            encodedPixels.Record(timestamp, wrotePixels);


            jobTimes.Report(job.TotalTicks);
            decodeTimes.Report(job.DecodeTicks);
            encodeTimes.Report(job.EncodeTicks);
            jobOtherTime.Report(job.TotalTicks - job.DecodeTicks - job.EncodeTicks);

            if (job.SourceFileExtension != null)
            {
                var ext = job.SourceFileExtension.ToLowerInvariant().TrimStart('.');
                counters.Increment("source_file_ext_" + ext);
            }

            PostJobQuery(job.FinalCommandKeys);

            NoticeDomains(job.ImageDomain, job.PageDomain);

        }


        readonly ConcurrentDictionary<string, DictionaryCounter<string>> uniques = new(StringComparer.Ordinal);
        
        // ReSharper disable once UnusedMethodReturnValue.Local
        private long CountLimitedUniqueValuesIgnoreCase(string category, string? value, int limit, string otherBucketValue)
        {
            return uniques.GetOrAdd(category, (_) =>
                new DictionaryCounter<string>(limit, otherBucketValue, StringComparer.OrdinalIgnoreCase))
                .Increment(value ?? "null");

        }
        private IEnumerable<string> GetPopularUniqueValues(string category, int limit)
        {
            return uniques.TryGetValue(category, out DictionaryCounter<string>? v) ?
                    v.GetCounts()
                    .Where(pair => pair.Value > 0)
                    .OrderByDescending(pair => pair.Value)
                    .Take(limit).Select(pair => pair.Key) :
                    Enumerable.Empty<string>();
        }

        private void NoticeDomains(string? imageDomain, string? pageDomain)
        {
            if (imageDomain != null) {
                CountLimitedUniqueValuesIgnoreCase("image_domains", imageDomain, 45, "_other_");
            }
            if (pageDomain != null)
            {
                CountLimitedUniqueValuesIgnoreCase("page_domains", pageDomain, 45, "_other_");
            }
        }

        private void PostJobQuery(IEnumerable<string>? querystringKeys)
        {
            if (querystringKeys == null) return;
            foreach (var key in querystringKeys)
            {
                if (key != null)
                {
                    CountLimitedUniqueValuesIgnoreCase("job_query_keys", key, 100, "_other_");
                }
            }
        }

        public void PreRewriteQuery(IEnumerable<string>? querystringKeys)
        {
            if (querystringKeys == null) return;
            foreach (var key in querystringKeys)
            {
                if (key != null)
                {
                    CountLimitedUniqueValuesIgnoreCase("original_query_keys", key, 100, "_other_");
                }
            }
        }

        public static void BlobRead(long ticks, long bytes)
        {
            Singleton.blobReadEvents.Record(Stopwatch.GetTimestamp(), 1);
            Singleton.blobReadBytes.Record(Stopwatch.GetTimestamp(), bytes);
            Singleton.blobReadTimes.Report(ticks);
        }

        public IInfoAccumulator GetReportPairs()
        {
            var q = new QueryAccumulator().Object;
            var timeThis = Stopwatch.StartNew();
            // Increment when we break the schema (or, as in v4, reduce the frequency)
            q.Add("reporting_version", 100);
            
            Process.Value.Add(q);
            Hardware.Value.Add(q);
            if (_lastInfoProviders != null)
            {
                foreach (var provider in _lastInfoProviders)
                {
                    provider?.Add(q);
                }
            }
            //Add counters
            foreach(var pair in counters.GetCounts()){
                q.Add(pair.Key, pair.Value.ToString());
            }

            //Add rates
            foreach(var rate in rates)
            {
                q.Add(rate.Key + "_total", rate.Value.RecordedTotal);
                foreach (var pair in rate.Value.GetStats())
                {
                    var baseKey = rate.Key + "_per_" + pair.Interval.Unit;
                    q.Add(baseKey + "_max", pair.Max);
                }
            }

            //Add percentiles
            foreach(var d in percentiles)
            {
                var values = d.Value.GetPercentiles(Percentiles.Select(p => p / 100.0f));
                q.Add(values.Zip(Percentiles, 
                    (result, percent) => 
                        new KeyValuePair<string, string>(
                            d.Key + "_" + percent.ToString() + "th", result.ToString())));
                
            }


            q.Add("image_domains",
               string.Join(",", GetPopularUniqueValues("image_domains", 8)));
            q.Add("page_domains",
                string.Join(",", GetPopularUniqueValues("page_domains", 8)));

            var originalKeys = GetPopularUniqueValues("original_query_keys", 40).ToArray();

            q.Add("query_keys",
                string.Join(",", originalKeys));
            q.Add("extra_job_query_keys",
                string.Join(",", GetPopularUniqueValues("job_query_keys", 40).Except(originalKeys).Take(2)));

            timeThis.Stop();
            collectInfoTimes.Report(timeThis.ElapsedTicks);
            return q;
        }

        public void TrackRate(string eventCategoryKey, long count)
        {
            rates.GetOrAdd(eventCategoryKey, (_) => new MultiIntervalStats(Intervals)).Record(Stopwatch.GetTimestamp(), count);
        }

        public void IncrementCounter(string key)
        {
           counters.Increment(key);
        }
    }

}
