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
using Newtonsoft.Json;

namespace Imageflow.Server
{
    internal class ImageJobInfo
    {
        public ImageJobInfo(HttpContext context, ImageflowMiddlewareOptions options, BlobProvider blobProvider)
        {
            Authorized = ProcessRewritesAndAuthorization(context, options);

            if (!Authorized) return;
      
            HasParams = PathHelpers.SupportedQuerystringKeys.Any(FinalQuery.ContainsKey);


            var extension = Path.GetExtension(FinalVirtualPath);
            if (FinalQuery.TryGetValue("format", out var newExtension))
            {
                extension = newExtension;
            }

            EstimatedFileExtension = PathHelpers.SanitizeImageExtension(extension);
            
            primaryBlob = new BlobFetchCache(FinalVirtualPath, blobProvider);
            allBlobs = new List<BlobFetchCache>(1) {primaryBlob};

            if (HasParams)
            {
                CommandString = PathHelpers.SerializeCommandString(FinalQuery);
                   
                // Look up watermark names
                if (FinalQuery.TryGetValue("watermark", out var watermarkValues))
                {
                    var watermarkNames = watermarkValues.Split(",").Select(s => s.Trim(' '));
                    
                    appliedWatermarks = new List<NamedWatermark>();
                    foreach (var name in watermarkNames)
                    {
                        var watermark = options.NamedWatermarks.FirstOrDefault(w =>
                            w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (watermark == null)
                        {
                            throw new InvalidOperationException($"watermark {name} was referenced from the querystring but no watermark by that name is registered with the middleware");
                        }
                        
                        appliedWatermarks.Add(watermark);
                        allBlobs.Add(new BlobFetchCache(watermark.VirtualPath, blobProvider));
                    }
                }
            }
            
            provider = blobProvider;
        }

        public string FinalVirtualPath { get; private set; }
        
        public Dictionary<string,string> FinalQuery { get; private set; }
        public bool HasParams { get; }
        
        public bool Authorized { get; }
        public string CommandString { get; } = "";
        public string EstimatedFileExtension { get; }

        private readonly BlobProvider provider;

        private readonly List<NamedWatermark> appliedWatermarks;

        private readonly List<BlobFetchCache> allBlobs;
        private readonly BlobFetchCache primaryBlob;


        private bool ProcessRewritesAndAuthorization(HttpContext context, ImageflowMiddlewareOptions options)
        {
            var path = context.Request.Path.Value;
            var args = new UrlEventArgs(context, context.Request.Path.Value, PathHelpers.ToQueryDictionary(context.Request.Query));
            foreach (var handler in options.PreRewriteAuthorization)
            {
                var matches = string.IsNullOrEmpty(handler.PathPrefix) ||
                              path.StartsWith(handler.PathPrefix, StringComparison.OrdinalIgnoreCase);
                if (matches && !handler.Handler(args)) return false;
            }
            foreach (var handler in options.Rewrite)
            {
                var matches = string.IsNullOrEmpty(handler.PathPrefix) ||
                              path.StartsWith(handler.PathPrefix, StringComparison.OrdinalIgnoreCase);
                if (matches)
                {
                    handler.Handler(args);
                    path = args.VirtualPath;
                }
            }
            // Set defaults if keys are missing
            foreach (var pair in options.CommandDefaults)
            {
                if (!args.Query.ContainsKey(pair.Key))
                {
                    args.Query[pair.Key] = pair.Value;
                }
            }
            foreach (var handler in options.PreRewriteAuthorization)
            {
                var matches = string.IsNullOrEmpty(handler.PathPrefix) ||
                              path.StartsWith(handler.PathPrefix, StringComparison.OrdinalIgnoreCase);
                if (matches && !handler.Handler(args)) return false;
            }

            FinalVirtualPath = args.VirtualPath;
            FinalQuery = args.Query;
            return true; 
        }
        
       
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

        private string[] serializedWatermarkConfigs = null;
        private IEnumerable<string> SerializeWatermarkConfigs()
        {
            if (serializedWatermarkConfigs != null) return serializedWatermarkConfigs;
            if (appliedWatermarks == null) return Enumerable.Empty<string>();
            serializedWatermarkConfigs = appliedWatermarks.Select(w => w.Serialized()).ToArray();
            return serializedWatermarkConfigs;
        }

        public async Task<string> GetFastCacheKey()
        {
            // Only get DateTime values from local files
            var dateTimes = await Task.WhenAll(
                allBlobs
                    .Where(b => b.GetBlobResult()?.IsFile == true)
                    .Select(async b =>
                        (await b.GetBlob())?.LastModifiedDateUtc?.ToBinary().ToString()));
            
            return HashStrings(new string[] {FinalVirtualPath, CommandString}.Concat(dateTimes).Concat(SerializeWatermarkConfigs()));
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
            
            return HashStrings(new string[] {FinalVirtualPath, CommandString}.Concat(dateTimes).Concat(SerializeWatermarkConfigs()));
        }

        public async Task<ImageData> ProcessUncached()
        {
            //Fetch all blobs simultaneously
            var blobs = await Task.WhenAll(
                allBlobs
                    .Select(async b =>
                        (await b.GetBlob())));
            
            //Add all StreamSources
            List<InputWatermark> watermarks = null;
            if (appliedWatermarks != null)
            {
                watermarks = new List<InputWatermark>(appliedWatermarks.Count);
                watermarks.AddRange(
                    appliedWatermarks.Select((t, i) =>
                    {
                        if (blobs[i + 1] == null)
                            throw new BlobMissingException(
                                $"Cannot locate watermark \"{t.Name}\" at virtual path \"{t.VirtualPath}\"");
                        return new InputWatermark(
                            new StreamSource(blobs[i + 1].OpenRead(), true),
                            t.Watermark);
                    }));
            }

            using var buildJob = new FluentBuildJob();
            var jobResult = await buildJob.BuildCommandString(
                    new StreamSource(blobs[0].OpenRead(), true),
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
