namespace Imazen.Common.Licensing
{

    [AttributeUsage(AttributeTargets.Assembly)]
    [Obsolete("Use Imazen.Abstractions.AssemblyAttributes.CommitAttribute instead")]
    public class CommitAttribute : Abstractions.AssemblyAttributes.CommitAttribute
    {
        public CommitAttribute()
        {}

        public CommitAttribute(string commitId):base(commitId){}
    }
}