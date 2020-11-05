using System;

namespace Imazen.Common.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildDateAttribute : Attribute
    {

        string str;
        public BuildDateAttribute() { str = string.Empty; }
        public BuildDateAttribute(string txt) { str = txt; }

        public string Value { get { return str; } }

        public DateTimeOffset? ValueDate
        {
            get
            {
                DateTimeOffset v;
                if (DateTimeOffset.TryParse(str, null, System.Globalization.DateTimeStyles.RoundtripKind, out v))
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
            return str;
        }

    }
}