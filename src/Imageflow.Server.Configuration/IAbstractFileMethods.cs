namespace Imageflow.Server.Configuration
{
    /// <summary>
    /// Allows testing of config validation and interpolation without a filesystem - or for use of the config classes
    /// for out-of-place validation
    /// </summary>
    public interface IAbstractFileMethods
    {
        string ReadAllText(string path);
        bool FileExists(string path);
        bool DirectoryExists(string path);

    }
}