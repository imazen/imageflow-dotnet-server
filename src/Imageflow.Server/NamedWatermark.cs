using System.IO;
using Imageflow.Fluent;
using Newtonsoft.Json;

namespace Imageflow.Server
{
    public class NamedWatermark
    {
        public NamedWatermark(string name, string virtualPath, WatermarkOptions watermark)
        {
            Name = name;
            VirtualPath = virtualPath;
            Watermark = watermark;
        }
        public string Name { get; }
        public string VirtualPath { get; }
        public WatermarkOptions Watermark { get; }
        
        
        private string serialized = null;

        internal string Serialized()
        {
            if (serialized != null) return serialized;
            using var writer = new StringWriter();
            writer.WriteLine(Name);
            writer.WriteLine(VirtualPath);
            JsonSerializer.Create().Serialize(writer, Watermark.ToImageflowDynamic(0));


            writer.Flush(); //Required or no bytes appear
            serialized = writer.ToString();
            return serialized;
        }
    }
}