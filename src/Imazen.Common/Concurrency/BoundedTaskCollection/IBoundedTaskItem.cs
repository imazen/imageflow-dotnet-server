namespace Imazen.Common.Concurrency.BoundedTaskCollection {

    public interface IBoundedTaskItem{
        string UniqueKey { get; }

        // Can only be called once
        void StoreStartedTask(Task task);
        Task? GetTask();
        long GetTaskSizeInMemory();
    }
}