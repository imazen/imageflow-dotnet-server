using System;
using System.Reflection;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;

namespace Imazen.Common.Instrumentation
{
    class BasicProcessInfo
    {
        public static Guid ProcessGuid { get; } = Guid.NewGuid();

        public static int ProcessId { get; } = System.Diagnostics.Process.GetCurrentProcess().Id;

        public bool Process64Bit { get; } = Environment.Is64BitProcess;
        
        
        public BasicProcessInfo() { }

        public void Add(IInfoAccumulator query)
        {
            var q = query.WithPrefix("proc_");
            q.Add("64", Process64Bit);
            q.Add("guid", ProcessGuid);
            q.Add("id_hash", Utilities.Sha256TruncatedBase64(ProcessId.ToString(), 6));
            
            q.Add("working_set_mb", Environment.WorkingSet / 1000000);
           
            // TODO: check for mismatched assemblies?
        }
    }
}

