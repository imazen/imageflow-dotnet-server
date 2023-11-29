namespace Imazen.Routing.Helpers;

public static class ConcurrencyHelpers
{
    /// <summary>
    /// Bubbles up exceptions and cancellations, and returns the first task that matches the predicate.
    /// If no task matches, returns default(TRes)
    /// </summary>
    /// <param name="allTasks"></param>
    /// <param name="predicate"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TRes"></typeparam>
    /// <returns></returns>
    public static Task<TRes?> WhenAnyMatchesOrDefault<TRes>(
        List<Task<TRes>> allTasks,
        Func<TRes, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        if (allTasks.Count == 0)
        {
            throw new ArgumentException("At least one task must be provided", nameof(allTasks));
        }
        var taskCompletionSource = new TaskCompletionSource<TRes?>();
        var cancellationRegistration = cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());
        var allTasksCount = allTasks.Count;
        // We can use the same closure for every task, since it doesn't capture any task-specific state,
        // just thee predicate, taskCompletionSource, cancellationRegistration, and allTasksCount
        Action<Task<TRes>> continuation = t =>
        {
            if (t.IsFaulted)
            {
                //We don't touch exceptions; the caller can iterate the tasks to find exceptions
                //taskCompletionSource.TrySetException(t.Exception!.InnerExceptions);
            }
            else if (t.IsCanceled)
            {
                // The caller handles all cancellations. They give us tasks preconfigured with cancellation tokens
                // the caller controls. Our cancellation token is just a signal to stop waiting for results.
                // taskCompletionSource.TrySetCanceled();
            }
            else if (predicate(t.Result))
            {
                taskCompletionSource.TrySetResult(t.Result);
            }

            // if all tasks have failed to match, we'll set the result to null
            if (Interlocked.Decrement(ref allTasksCount) == 0)
            {
                // We can de-register listening for cancellation, since we're done
                cancellationRegistration.Dispose();
                taskCompletionSource.TrySetResult(default);
                
            }
        };

        // run all simultaneously, and set the result when one completes and matches the predicate
        foreach (var task in allTasks)
        {
            // I guess it get scheduled anyway?
            task.ContinueWith(continuation, cancellationToken, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return taskCompletionSource.Task;
    }


    /// <summary>
    /// This alternate implementation uses Task.Any in a loop
    /// </summary>
    public static async Task<TRes?> WhenAnyMatchesOrDefault2<TRes>(
        List<Task<TRes>> allTasks,
        Func<TRes, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var completedTasks = allTasks.Where(t => t.IsCompleted).ToList();
            var match = completedTasks.FirstOrDefault(
                t => t.Status == TaskStatus.RanToCompletion && predicate(t.Result));
            if (match != null)
            {
                return match.Result;
            }
        
            // If all tasks are completed (faulted, cancelled, or success), and none match, return default
            var incompleteTasks = allTasks.Where(t => !t.IsCompleted).ToList();
            if (incompleteTasks.Count == 0)
            {
                return default;
            }

            // Wait for any task to complete
            var taskComplete = await Task.WhenAny(incompleteTasks).ConfigureAwait(false);
            // If it didn't succeed and meet criteria, try again 
            if (taskComplete.Status != TaskStatus.RanToCompletion)
                continue;
            // If it did succeed and meet criteria, return the result
            var result = taskComplete.Result;
            if (predicate(result))
            {
                return result;
            }

        }

    }

}