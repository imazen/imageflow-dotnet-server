namespace Imazen.Common.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class EditionAttribute : Attribute
    {
        private readonly string edition;

        public EditionAttribute()
        {
            edition = string.Empty;
        }

        public EditionAttribute(string editionString)
        {
            edition = editionString;
        }

        public string Value => edition;

        public override string ToString()
        {
            return edition;
        }
    }
}