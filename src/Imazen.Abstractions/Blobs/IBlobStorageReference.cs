using Imazen.Common.Extensibility.Support;

namespace Imazen.Abstractions.Blobs
{
    public interface IBlobStorageReference : IEstimateAllocatedBytesRecursive
    {

        /// <summary>
        /// Should include all information needed to uniquely identify the resource, such as the container/region/path, or computer/drive/path, etc.
        /// </summary>
        string GetFullyQualifiedRepresentation();
    }
}