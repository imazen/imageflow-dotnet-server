namespace Imazen.Common.Concurrency
{
    public sealed class BasicAsyncLock
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> releaser;

        public BasicAsyncLock()
        {
            releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
        {
            var wait = semaphore.WaitAsync(cancellationToken);
            return wait.IsCompleted ?
                        releaser :
                        wait.ContinueWith((_, state) => (IDisposable)state!,
                            releaser.Result, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        public Task<IDisposable> LockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var wait = semaphore.WaitAsync(timeout, cancellationToken);
            return wait.IsCompleted ?
                releaser :
                wait.ContinueWith((_, state) => (IDisposable)state!,
                    releaser.Result, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        
        private sealed class Releaser : IDisposable
        {
            private readonly BasicAsyncLock asyncLock;
            internal Releaser(BasicAsyncLock toRelease) { asyncLock = toRelease; }
            public void Dispose() { asyncLock.semaphore.Release(); }
        }
    }
}
