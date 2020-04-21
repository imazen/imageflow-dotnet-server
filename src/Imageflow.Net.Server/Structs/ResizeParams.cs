namespace Imageflow.Server.Structs
{
    public struct ResizeParams
    {
        public bool hasParams;
        public string commandString;

        public override string ToString()
        {
            return commandString;
        }
    }
}
