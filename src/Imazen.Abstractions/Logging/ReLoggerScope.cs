namespace Imazen.Abstractions.Logging
{
    internal sealed class ReLoggerScope<TState> : IDisposable  where TState : notnull
    {
        internal ReLoggerScope(IDisposable impl, TState state, ReLogger parent)
        {
            this.impl = impl;
            this.parent = parent;
            this.State = state;
        }
        
        private readonly IDisposable impl;
        private readonly ReLogger parent;

        private TState State { get; }

        public void Dispose()
        {
            impl.Dispose();
            parent.EndScope(State, this);
        }

        public override string ToString()
        {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            return State.ToString() ?? "";
        }
    }
}