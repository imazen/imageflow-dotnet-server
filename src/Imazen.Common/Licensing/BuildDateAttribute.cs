namespace Imazen.Common.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly)]
    [Obsolete("Use Imazen.Abstractions.AssemblyAttributes.BuildDateAttribute instead")]
    public class BuildDateAttribute : Abstractions.AssemblyAttributes.BuildDateAttribute
    {
        public BuildDateAttribute()
        { }
        public BuildDateAttribute(string buildDateStringRoundTrip):base(buildDateStringRoundTrip) { }
        
    }
}