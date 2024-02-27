using Microsoft.Extensions.Logging;

namespace Imazen.Abstractions.Logging
{
    internal class ReLogger : IReLogger
    {
        private readonly ILogger impl;
        private readonly bool retain;
        private readonly string? retainUniqueKey;
        private readonly ReLoggerFactory parent;
        private readonly string categoryName;
        
        internal ReLogger(ILogger impl, ReLoggerFactory parent, string categoryName)
        {
            this.impl = impl;
            this.parent = parent;
            this.retain = false;
            this.retainUniqueKey = null;
            this.categoryName = categoryName;
        }
        private ReLogger(ILogger impl, ReLoggerFactory parent, string categoryName, bool retain, string? retainUniqueKey)
        {
            this.impl = impl;
            this.retain = retain;
            this.parent = parent;
            this.retainUniqueKey = retainUniqueKey;
            this.categoryName = categoryName;
        }
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            parent.Log(categoryName, logLevel, eventId, state, exception, formatter, retain, retainUniqueKey);
            impl.Log(logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return impl.IsEnabled(logLevel);
        }

 
        private IReLogger? withRetain = null;
        public IReLogger WithRetain
        {
            get
            {
                withRetain ??= new ReLogger(impl, parent, categoryName, true, null);
                return withRetain;
            }
        }

        public IReLogger WithRetainUnique(string key)
        {
            return new ReLogger(impl, parent, categoryName, true, key);
        }
        
        public IReLogger WithSubcategory(string subcategoryString)
        {
            return new ReLogger(impl, parent, $@"{categoryName}>{subcategoryString}", retain, retainUniqueKey);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var instance = new ReLoggerScope<TState>(impl.BeginScope(state), state, this);
            parent.BeginScope(state, instance);
            return instance;
        }
        internal void EndScope<TState>(TState state, ReLoggerScope<TState> scope) where TState : notnull
        {
            parent.EndScope(state, scope);
        }
    }
}