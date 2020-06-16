using System;
using System.Collections.Generic;

namespace Imageflow.Server
{
    public enum PresetPriority
    {
        DefaultValues,
        OverrideQuery
    }
    public class PresetOptions
    {
        public string Name { get; }
        
        public PresetPriority Priority { get;  }
        
        internal Dictionary<string, string> pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        public PresetOptions(string name, PresetPriority priority)
        {
            Name = name;
            Priority = priority;
        }

        public PresetOptions SetCommand(string key, string value)
        {
            pairs[key] = value;
            return this;
        }
    }
}