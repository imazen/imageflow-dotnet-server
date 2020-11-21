using System;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.Common.Concurrency
{
    public class DisposableAsyncLock
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> releaser;

        public DisposableAsyncLock()
        {
            releaser = Task.FromResult((IDisposable) new Releaser(this));
        }

        public Task<IDisposable> LockAsync()
        {
            var wait = semaphore.WaitAsync();
            return wait.IsCompleted
                ? releaser
                : wait.ContinueWith((_, state) => (IDisposable) state,
                    releaser.Result, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly DisposableAsyncLock toRelease;

            internal Releaser(DisposableAsyncLock toRelease)
            {
                this.toRelease = toRelease;
            }

            public void Dispose()
            {
                toRelease.semaphore.Release();
            }
        }

    }
}