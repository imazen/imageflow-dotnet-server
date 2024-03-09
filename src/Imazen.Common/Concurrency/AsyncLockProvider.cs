﻿/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using Imazen.Abstractions.Resulting;
using InvalidOperationException = System.InvalidOperationException;

namespace Imazen.Common.Concurrency {
    
    /// <summary>
    /// Provides lightweight locking based on a string key. Useful for fixing the thundering herd problem. 
    /// Locks are local to the LockProvider instance.
    /// The class handles disposing of unused locks. Generally used for 
    /// coordinating writes to files (of which there can be millions). 
    /// Only keeps key/lock pairs in memory which are in use.
    /// Thread-safe.
    /// Uses SemaphoreSlim instead of locks to be thread-context agnostic.
    /// </summary>
    public class AsyncLockProvider {

    

        /// <summary>
        /// The only objects in this collection should be for open files. 
        /// </summary>
        private readonly Dictionary<string, SemaphoreSlim> locks = 
                        new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        /// <summary>
        /// Synchronization object for modifications to the 'locks' dictionary
        /// </summary>
        private readonly object createLock = new object();

        internal int GetActiveLockCount()
        {
            lock (createLock)
            {
                return locks.Count;
            }
        }

        /// <summary>
        /// Returns true if the given key *might* be locked.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool MayBeLocked(string key)
        {
            lock (createLock)
            {
                return locks.ContainsKey(key);
            }
        }

        /// <summary>
        /// A synchronous wrapper for TryExecuteAsync.
        /// Attempts to execute the 'success' callback inside a lock based on 'key'.  If successful, returns true.
        /// If the lock cannot be acquired within 'timeoutMs' or cancellation token is triggered, returns false
        /// In a worst-case scenario, it could take up to twice as long as 'timeoutMs' to return false.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="success"></param>
        /// <returns></returns>
        public bool TryExecuteSynchronous(string key, int timeoutMs, CancellationToken cancellationToken, Action success)
        {
            var task = TryExecuteAsync(key, timeoutMs, cancellationToken, () => { success();  return Task.FromResult(false); });
            task.RunSynchronously();
            task.Wait(cancellationToken);
            return task.Result;
        }

        /// <summary>
        /// Attempts to execute the 'success' callback inside a lock based on 'key'.  If successful, returns true.
        /// If the lock cannot be acquired within 'timeoutMs' or cancellation token is triggered, returns false
        /// In a worst-case scenario, it could take up to twice as long as 'timeoutMs' to return false.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="success"></param>
        /// <param name="timeoutMs"></param>
        public async Task<bool> TryExecuteAsync(string key, int timeoutMs, CancellationToken cancellationToken,
            Func<Task> success)
        {
            var result = await TryExecuteAsync<Empty, Empty>(key, timeoutMs, cancellationToken, Empty.Value ,async (Empty e, CancellationToken ct) => {
                await success();
                return Empty.Value;
            });
            return result.IsOk;
        }


        /// <summary>
        /// Attempts to execute the 'T work(TP param, CancellationToken ct)' callback inside a lock based on 'key'.  If successful, returns an OK result wrapping the return
        /// value. CancellationToken is passed through.
        /// If the lock cannot be acquired within 'timeoutMs', returns false
        /// In a worst-case scenario, it could take up to twice as long as 'timeoutMs' to return false.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="workParameter"></param>
        /// <param name="work"></param>
        /// <param name="lockTimeoutMs"></param>
        /// 
        public async Task<IResult<T,Empty>> TryExecuteAsync<T,TP>(string key, int lockTimeoutMs, CancellationToken cancellationToken, TP workParameter, Func<TP,CancellationToken,Task<T>> work)
        {
            //Record when we started. We don't want an infinite loop.
            DateTime startedAt = DateTime.UtcNow;

            // Tracks whether the lock acquired is still correct
            bool validLock = true; 
            // The lock corresponding to 'key'
            SemaphoreSlim? itemLock = null;

            try {
                //We have to loop until we get a valid lock and it stays valid until we lock it.
                do {
                    // 1) Creation/acquire phase
                    lock (createLock) {
                        // We have to lock on dictionary writes, since otherwise 
                        // two locks for the same file could be created and assigned
                        // at the same time. (i.e, between TryGetValue and the assignment)
                        if (!locks.TryGetValue(key, out itemLock))
                            locks[key] = itemLock = new SemaphoreSlim(1); //make a new lock!

                    }
                    // Loophole (part 1):
                    // Right here - this is where another thread (executing part 2) could remove 'itemLock'
                    // from the dictionary, and potentially, yet another thread could 
                    // insert a new value for 'itemLock' into the dictionary... etc, etc..

                    // 2) Execute phase
                    if (await itemLock.WaitAsync(lockTimeoutMs, cancellationToken)) {
                        try {
                            // May take minutes to acquire this lock. 

                            // Trying to detect an occurrence of loophole above
                            // Check that itemLock still exists and matches the dictionary
                            lock (createLock) {
                                validLock = locks.TryGetValue(key, out var newLock);
                                validLock = validLock && newLock == itemLock;
                            }
                            // Only run the callback if the lock is valid
                            if (validLock) {
                                return Result<T,Empty>.Ok(await work(workParameter, cancellationToken)); // Extremely long-running callback, perhaps throwing exceptions
                            }
                            // if control reaches here, the lock is invalid, and we should try again via the outer loop
                        } finally {
                            itemLock.Release();
                        }
                    } else {
                        validLock = false; //So the finally clause doesn't try to clean up the lock, someone else will do that.
                        return Result<T,Empty>.Err(); //Someone else had the lock, they can clean it up.
                    }

                    //Are we out of time, still having an invalid lock?
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (!validLock && Math.Abs(DateTime.UtcNow.Subtract(startedAt).TotalMilliseconds) > lockTimeoutMs) {
                        //We failed to get a valid lock in time. 
                        return Result<T,Empty>.Err();
                    }


                    // If we had an invalid lock, we have to try everything over again.
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                } while (!validLock);
            } finally {
                if (validLock) {
                    // Loophole (part 2). When loophole part 1 and 2 cross paths,
                    // An lock object may be removed before being used, and be orphaned

                    // 3) Cleanup phase - Attempt cleanup of lock objects so we don't 
                    //   have a *very* large and slow dictionary.
                    lock (createLock) {
                        //  TryEnter() fails instead of waiting. 
                        //  A normal lock would cause a deadlock with phase 2. 
                        //  Specifying a timeout would add great and pointless overhead.
                        //  Whoever has the lock will clean it up also.

                        // It succeeds, so no-one else is working on it 
                        // (but may be preparing to, see loophole)
                        // Only remove the lock object if it 
                        // still exists in the dictionary as-is
                        if (itemLock != null && itemLock.CurrentCount > 0 &&
                            locks.TryGetValue(key, out var existingLock)
                            && existingLock == itemLock)
                        {
                            locks.Remove(key);
                        }
                    }
                }
            }
            // Ideally the only objects in 'locks' will be open operations now.
            //This should be impossible to reach, the loop only exits when validLock is true.
            //And if validLock equals true, then the function will return
            // otherwise an exception is thrown.
            throw new InvalidOperationException("Unreachable code");
        }
    }
}
