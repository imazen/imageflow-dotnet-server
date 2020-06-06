namespace Imageflow.Server.Structs
{
    public struct ResizeParams
    {
        public bool HasParams;
        public string CommandString;
        public string EstimatedFileExtension;

        public override string ToString()
        {
            return CommandString;
        }
    }
}
