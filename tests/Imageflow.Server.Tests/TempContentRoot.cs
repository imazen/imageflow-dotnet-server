using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace Imageflow.Server.Tests
{
  
    internal class TempContentRoot: IDisposable
    {
        public string PhysicalPath { get; }

        public TempContentRoot()
        {
            PhysicalPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString().ToLowerInvariant());
            Directory.CreateDirectory(PhysicalPath);
        }

        public byte[] GetResourceBytes(string resourceName)
        {
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            using var reader = embeddedProvider.GetFileInfo(resourceName).CreateReadStream();
            return ReadToEnd(reader);
        }
        public TempContentRoot AddResource(string relativePath, string resourceName)
        {
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            using var reader = embeddedProvider.GetFileInfo(resourceName).CreateReadStream();
            var newFilePath = Path.Combine(PhysicalPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var parentDir = Path.GetDirectoryName(newFilePath);
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);
            using var newFile = File.Create(newFilePath);
            reader.CopyTo(newFile);
            return this;
        }

        public void Dispose()
        {
            Directory.Delete(PhysicalPath, true);
        }
        
        public static byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if(stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if(stream.CanSeek)
                {
                    stream.Position = originalPosition; 
                }
            }
        }
    }
}