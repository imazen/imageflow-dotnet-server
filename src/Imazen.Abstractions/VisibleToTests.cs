using System.Runtime.CompilerServices;

// We are exposing PolySharp once
[assembly: InternalsVisibleTo("ImazenShared.Tests")]
[assembly: InternalsVisibleTo("Imazen.Common")]
[assembly: InternalsVisibleTo("Imazen.Routing")]
[assembly: InternalsVisibleTo("Imageflow.Server.Storage.AzureBlob")]
[assembly: InternalsVisibleTo("Imageflow.Server.Storage.S3")]
[assembly: InternalsVisibleTo("Imageflow.Server.Storage.AzureBlob.Tests")]
[assembly: InternalsVisibleTo("Imageflow.Server.Storage.RemoteReader")]
[assembly: InternalsVisibleTo("Imazen.HybridCache")]


[assembly: InternalsVisibleTo("Imageflow.Server")]
[assembly: InternalsVisibleTo("Imageflow.Server.Tests")]
[assembly: InternalsVisibleTo("ImageResizer")]
[assembly: InternalsVisibleTo("ImageResizer.LicensingTests")]
[assembly: InternalsVisibleTo("ImageResizer.HybridCache.Tests")]
[assembly: InternalsVisibleTo("ImageResizer.HybridCache.Benchmark")]
