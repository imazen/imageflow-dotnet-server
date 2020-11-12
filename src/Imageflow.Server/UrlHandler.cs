namespace Imageflow.Server
{
    internal class UrlHandler<T>
    {
        internal UrlHandler(string prefix, T handler)
        {
            PathPrefix = prefix;
            Handler = handler;
        }
        public string PathPrefix { get; }
        
        public T Handler { get; }
    }
}