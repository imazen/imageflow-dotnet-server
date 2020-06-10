using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Imageflow.Fluent;
using Imazen.Common.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Imageflow.Server
{
    internal class ImageJobInfo
    {
        public ImageJobInfo(string virtualPath, IQueryCollection query,
            IReadOnlyCollection<NamedWatermark> namedWatermarks, BlobProvider blobProvider)
        {
            HasParams = PathHelpers.SupportedQuerystringKeys.Any(query.ContainsKey);


            var extension = Path.GetExtension(virtualPath);
            if (query.TryGetValue("format", out var newExtension))
            {
                extension = newExtension;
            }

            EstimatedFileExtension = PathHelpers.SanitizeImageExtension(extension);
            
            primaryBlob = new BlobFetchCache(virtualPath, blobProvider);
            allBlobs = new List<BlobFetchCache>(1) {primaryBlob};

            if (HasParams)
            {
                CommandString = string.Join("&", PathHelpers.MatchingResizeQueryStringParameters(query));
                
                // Look up watermark names
                if (query.TryGetValue("watermark", out var watermarkValues))
                {
                    var watermarkNames = watermarkValues.SelectMany(w => w.Split(",")).Select(s => s.Trim(' '));
                    
                    appliedWatermarks = new List<NamedWatermark>();
                    foreach (var name in watermarkNames)
                    {
                        var watermark = namedWatermarks.FirstOrDefault(w =>
                            w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (watermark == null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(query), $"watermark {name} was referenced from the querystring but no watermark by that name is registered with the middleware");
                        }
                        
                        appliedWatermarks.Add(watermark);
                        allBlobs.Add(new BlobFetchCache(watermark.VirtualPath, blobProvider));
                    }
                }
            }

            VirtualPath = virtualPath;
            provider = blobProvider;
        }

        public string VirtualPath { get; }
        public bool HasParams { get; }
        public string CommandString { get; }
        public string EstimatedFileExtension { get; }

        private readonly BlobProvider provider;

        private readonly List<NamedWatermark> appliedWatermarks;

        private readonly List<BlobFetchCache> allBlobs;
        private readonly BlobFetchCache primaryBlob;
        

       
        
       
        public bool PrimaryBlobMayExist()
        {
            return primaryBlob.GetBlobResult() != null;
        }
        
      
        public Task<IBlobData> GetPrimaryBlob()
        {
            return primaryBlob.GetBlob();
        }

        private string HashStrings(IEnumerable<string> strings)
        {
            return PathHelpers.Base64Hash(string.Join('|',strings));
        }
        public async Task<string> GetFastCacheKey()
        {
            // Only get DateTime values from local files
            var dateTimes = await Task.WhenAll(
                allBlobs
                    .Where(b => b.GetBlobResult()?.IsFile == true)
                    .Select(async b =>
                        (await b.GetBlob())?.LastModifiedDateUtc?.ToBinary().ToString()));
            
            return HashStrings(new string[] {VirtualPath, CommandString}.Concat(dateTimes));
        }

        public override string ToString()
        {
            return CommandString;
        }

        public async Task<string> GetExactCacheKey()
        {
            var dateTimes = await Task.WhenAll(
                allBlobs
                    .Select(async b =>
                        (await b.GetBlob())?.LastModifiedDateUtc?.ToBinary().ToString()));
            
            return HashStrings(new string[] {VirtualPath, CommandString}.Concat(dateTimes));
        }

        public async Task<ImageData> ProcessUncached()
        {
            //Fetch all blobs simultaneously
            var blobs = await Task.WhenAll(
                allBlobs
                    .Select(async b =>
                        (await b.GetBlob())));
            
            //Add all StreamSources
            var watermarks = new List<InputWatermark>(appliedWatermarks.Count);
            watermarks.AddRange(
                appliedWatermarks.Select((t, i) => 
                    new InputWatermark(
                        new StreamSource(blobs[i + 1].OpenReadAsync(), true),
                        t.Watermark)));

            using var buildJob = new FluentBuildJob();
            var jobResult = await buildJob.BuildCommandString(
                    new StreamSource(blobs[0].OpenReadAsync(), true),
                    new BytesDestination(), CommandString, watermarks)
                .Finish()
                .InProcessAsync();

            var bytes = jobResult.First.TryGetBytes().Value;

            return new ImageData
            {
                ContentType = jobResult.First.PreferredMimeType,
                FileExtension = jobResult.First.PreferredExtension,
                ResultBytes = bytes
            };
        }
    }
}
