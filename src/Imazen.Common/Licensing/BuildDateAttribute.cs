using System;

namespace Imazen.Common.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildDateAttribute : Attribute
    {
        public BuildDateAttribute() { Value = string.Empty; }
        public BuildDateAttribute(string buildDateStringRoundTrip) { Value = buildDateStringRoundTrip; }

        public string Value { get; }

        public DateTimeOffset? ValueDate
        {
            get
            {
                DateTimeOffset v;
                if (DateTimeOffset.TryParse(Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out v))
                {
                    return v;
                }else
                {
                    return null;
                }
            }
        }

        public override string ToString()
        {
            return Value;
        }

    }
}