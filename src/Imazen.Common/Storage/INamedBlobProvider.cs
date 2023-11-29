using Imazen.Abstractions;

namespace Imazen.Common.Storage
{
    [Obsolete("This type has moved to the Imazen.Abstractions.Blobs.LegacyProviders namespace.")]
    public interface INamedBlobProvider : IBlobProvider, IUniqueNamed
    {
        
    }
}