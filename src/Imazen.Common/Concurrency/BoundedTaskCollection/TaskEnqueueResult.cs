namespace Imazen.Common.Concurrency.BoundedTaskCollection;

public enum TaskEnqueueResult
{
    Enqueued,
    AlreadyPresent,
    QueueFull,
    Stopped
}