using System;

namespace Imazen.Common.Licensing
{

    [AttributeUsage(AttributeTargets.Assembly)]
    public class CommitAttribute : Attribute
    {
        private readonly string commitId;

        public CommitAttribute()
        {
            commitId = string.Empty;
        }

        public CommitAttribute(string commitId)
        {
            this.commitId = commitId;
        }

        public string Value => commitId;

        public override string ToString()
        {
            return commitId;
        }
    }
}