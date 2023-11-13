using System.Collections.Generic;

namespace Imazen.Common.BlobStorage
{

    internal interface IBlobMetadata
    {

        bool ValidateKey(string key, out string error);
        bool ValidateValue(string value, out string error);
        int SizeInBytes { get; }


        bool TryAdd(string key, string value, out string error);
        bool TrySet(string key, string value, out string error);
        void Set(string key, string value);
        void Remove(string key);

        bool TryGetValue (string key, out string value);
        string Get(string key);



        IReadOnlyDictionary<string, string> AsReadOnlyDictionary();
    }
}
