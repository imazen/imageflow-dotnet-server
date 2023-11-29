using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Unused
{
        public record ImageflowRoute(string pathPrefix, IImageSource imageSource)
        {
                public string PathPrefix { get; set; } = pathPrefix;

                public StringComparison PathPrefixComparison { get; set; } = StringComparison.Ordinal;

                // match extensionless?
                public bool HandleExtensionlessUrls { get; set; } = false;

                internal IList<StringSegment>? HostStringPatterns { get; }

                public ImageflowRouteOptions? Options { get; set; }

                public IImageSource ImageSource { get; set; } = imageSource;
        }

        public interface IImageSource
        {
        }
}