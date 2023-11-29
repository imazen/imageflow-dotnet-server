namespace Imazen.Abstractions.Resulting;


public interface IResult
{
    bool IsOk { get; }
    
    bool IsError { get; }
}

public interface IResult<out T> : IResult
{
    T? Value { get; }
}
public interface IResult<out T, out TE> : IResult<T>
{
    TE? Error { get; }
}

public interface IDisposableResult<out T, out TE> : IResult<T>, IDisposable
{
    TE? Error { get; }
}

