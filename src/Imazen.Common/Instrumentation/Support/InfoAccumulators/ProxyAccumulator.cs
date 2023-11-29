namespace Imazen.Common.Instrumentation.Support.InfoAccumulators
{
    internal class ProxyAccumulator : IInfoAccumulator
    {
        readonly Action<string, string?> add;
        readonly Action<string, string?> prepend;
        readonly bool usePrepend;
        readonly Func<IEnumerable<KeyValuePair<string, string?>>> fetch;
        public ProxyAccumulator(bool usePrepend, Action<string, string?> add, Action<string, string?> prepend, Func<IEnumerable<KeyValuePair<string, string?>>> fetch)
        {
            this.usePrepend = usePrepend;
            this.add = add;
            this.prepend = prepend;
            this.fetch = fetch;
        }
        public void AddString(string key, string? value)
        {
            if (usePrepend)
            {
                prepend(key, value);
            }
            else
            {
                add(key, value);
            }
        }

        public IEnumerable<KeyValuePair<string, string?>> GetInfo()
        {
            return fetch();
        }

        public IInfoAccumulator WithPrefix(string prefix)
        {
            return new ProxyAccumulator(usePrepend, (k, v) => add(prefix + k, v), (k, v) => prepend(prefix + k, v), fetch);
        }

        public IInfoAccumulator WithPrepend(bool prepending)
        {
            return new ProxyAccumulator(prepending, add, prepend, fetch);
        }
    }
}
