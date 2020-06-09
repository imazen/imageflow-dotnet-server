namespace Imageflow.Server
{
    internal struct ResizeParams
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
