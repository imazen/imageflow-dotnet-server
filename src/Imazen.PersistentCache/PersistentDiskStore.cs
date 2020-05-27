using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    public class PersistentDiskStore : IPersistentStore
    {
        readonly string cacheFolder; 
        public PersistentDiskStore(string cacheFolder)
        {
            this.cacheFolder = cacheFolder;
        }
        internal string GetPath(uint shardId, string key)
        {
            return Path.Combine(cacheFolder, shardId.ToString(), key.Replace('/', Path.DirectorySeparatorChar));
        }
        public Task Delete(uint shard, string key, CancellationToken cancellationToken)
        {
            try
            {
                File.Delete(GetPath(shard, key));
                return Task.CompletedTask;
            }
            catch (DirectoryNotFoundException)
            {
                return Task.CompletedTask;
            }
            catch (FileNotFoundException)
            {
                return Task.CompletedTask;
            }
        }

        private struct BlobFileInfo : IBlobInfo
        {
            public string KeyName { get; internal set; }

            public ulong SizeInBytes { get; internal set; }
        }
        public Task<IEnumerable<IBlobInfo>> List(uint shard, string parentKey, CancellationToken cancellationToken)
        {
            try
            {
                var info = new DirectoryInfo(GetPath(shard, parentKey));
                var files = info.GetFiles("*", SearchOption.TopDirectoryOnly);
                var infos = new List<IBlobInfo>(files.Length);
                foreach (var f in files)
                {
                    infos.Add(new BlobFileInfo { KeyName = $"{shard}/{parentKey.TrimEnd('/')}/{f.Name}", SizeInBytes = (ulong)f.Length });
                }
                return Task.FromResult<IEnumerable<IBlobInfo>>(infos);
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult(Enumerable.Empty<IBlobInfo>());
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult(Enumerable.Empty<IBlobInfo>());
            }
        }

        public Task<Stream> ReadStream(uint shard, string key, CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(File.OpenRead(GetPath(shard, key)) as Stream);
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult<Stream>(null);
            }catch (FileNotFoundException)
            {
                return Task.FromResult<Stream>(null); ;
            }
        }

        public async Task WriteBytes(uint shard, string key, byte[] data, CancellationToken cancellationToken)
        {
            var dirPath = Path.GetDirectoryName(GetPath(shard, key));
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            using (var stream = File.Create(GetPath(shard, key)))
            {
                await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                await stream.FlushAsync();
            }
        }
    }
}
