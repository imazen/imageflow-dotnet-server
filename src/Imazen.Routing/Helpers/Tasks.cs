using System.Runtime.CompilerServices;

namespace Imazen.Routing.Helpers;
internal static class Tasks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> ValueResult<T>(T t)
    {
#if DOTNET5_0_OR_GREATER
        return Tasks.ValueResult(t);
#else
        return new ValueTask<T>(t);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask ValueComplete()
    {
#if DOTNET5_0_OR_GREATER
        return Tasks.ValueResult(t);
#else
        return new ValueTask();
#endif
    }

    public static async ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks)
    {
        // We could alternately depend on https://www.nuget.org/packages/ValueTaskSupplement
        // https://github.com/Cysharp/ValueTaskSupplement/blob/master/src/ValueTaskSupplement/ValueTaskEx.WhenAll.tt
        return await Task.WhenAll(tasks.Select(x => x.AsTask()));
    }

}