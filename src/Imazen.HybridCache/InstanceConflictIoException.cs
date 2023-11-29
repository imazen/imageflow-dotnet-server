// delegate to an IOException, name it InstanceConflictIoException, include a parameter for the path and shardid that caused the conflict

namespace Imazen.HybridCache{

    public class ImageflowMultipleHybridCacheInstancesNotSupportedIoException : Exception
    {
        public ImageflowMultipleHybridCacheInstancesNotSupportedIoException(IOException original, string message, string path, int shardId) : base(message)
        {
            Original = original;
            Path = path;
            ShardId = shardId;
        }

        public IOException Original { get; }
        public string Path { get; }
        public int ShardId { get; }

        public override string ToString()
        {
            return $"{Message} Path: {Path} ShardId: {ShardId} IOException: {Original}";
        }
    }
}