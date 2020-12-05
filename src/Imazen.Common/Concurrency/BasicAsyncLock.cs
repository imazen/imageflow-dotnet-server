using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public Task<IDisposable> LockAsync()
        {
            var wait = semaphore.WaitAsync();
            return wait.IsCompleted ?
                        releaser :
                        wait.ContinueWith((_, state) => (IDisposable)state,
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
