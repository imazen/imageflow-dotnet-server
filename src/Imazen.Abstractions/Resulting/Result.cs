using System.Net;
using System.Runtime.InteropServices;
using Imazen.Abstractions.Blobs.LegacyProviders;

namespace Imazen.Abstractions.Resulting;



public class Result : IResult
{
    protected Result(bool ok)
    {
        IsOk = ok;
    }

    public bool IsOk { get; }
    public bool IsError => !IsOk;

    private static readonly Result SharedOk = new Result(true);


    public static Result Ok() => SharedOk;

    private static readonly Result SharedErr = new Result(false);
    public static Result Err() => SharedErr;
    
    public static implicit operator Result(bool ok) => new Result(ok);
    
    public static implicit operator bool(Result result) => result.IsOk;

    public override string ToString()
    {
        return IsOk ? "(Ok)" : "(Error)";
    }
    
    
    
}

//
// public class Result<T> : CodeResult<T> 
// {
//     
//     private Result(bool success, bool notImplemented, int statusCode, string? statusMessage, T? data, bool suppressDisposeData)
//     {
//         SuppressDisposeData = suppressDisposeData;
//         NotImplemented = notImplemented;
//         IsOk = success;
//         StatusCode = statusCode;
//         StatusMessage = statusMessage;
//         Value = data;
//         Enumerable.Empty<>()
//     }
//     
//     
//     
//     public bool IsOk { get; }
//     public bool NotImplemented { get; } = false;
//     public int StatusCode { get; }
//     public string? StatusMessage { get; } = null;
//     public T? Value { get; } = default(T);
//     
//     public bool SuppressDisposeData { get; }
//
//
//     public static CodeResult<T> NotImplementedResult(string message) => ErrorResult(501, message);
//     public static CodeResult<T> ErrorResult(int statusCode, string message) => new Result<T>(false, statusCode == 501, statusCode, message, default(T), true);
//
//     public static CodeResult<T> SuccessResult(T data, int statusCode, string message, bool suppressDisposeData) => new Result<T>(false, statusCode == 501, statusCode, message, data, suppressDisposeData);
//
//     public static CodeResult<T> NotFoundResult(string? message = null) => ErrorResult(404, message ?? "Not found");
//     
//     
//     
//     public void Dispose()
//     {
//         if (!SuppressDisposeData && Value is IDisposable d) d.Dispose();
//     }
//
//     public static CodeResult<T> ErrorResult(int statusCode) =>  new Result<T>(false, statusCode == 501, statusCode, null, default(T), true);
//     
// }

public class Result<T, TE> : IResult<T, TE>
{

    protected Result(bool isOk, T? okValue, TE? errorValue)
    {
        Value = okValue;
        IsOk = isOk;
        Error = errorValue;
        if (IsOk && Value == null) throw new ArgumentNullException(nameof(okValue));
        if (!IsOk && Error == null) throw new ArgumentNullException(nameof(errorValue));
    }
    protected Result(T okValue) : this(true, okValue, default(TE))
    {
    }
    protected Result(bool ignored, TE errorValue) : this(false, default(T), errorValue)
    {
    }
    
  
    protected internal Result()
    {
        
    }
    
    public bool IsError => !IsOk;
    public bool IsOk { get; protected set; }
    public T? Value { get; protected set; }
    public TE? Error { get; protected set; }
    
    public static Result<T, TE> Err(TE errorValue) => new Result<T, TE>(false, default(T), errorValue);

    
    public static Result<T, TE> Ok(T okValue) => new Result<T, TE>(okValue);
    
    private static readonly Result<T, Empty> SharedEmptyErr = new Result<T, Empty>(false, default(T), Empty.Value);
    public static Result<T, Empty> Err() => SharedEmptyErr;
    
    private static readonly Result<Empty, TE> SharedEmptyOk = new Result<Empty, TE>(Empty.Value);
    public static Result<Empty, TE> Ok() => SharedEmptyOk;
    
    public static implicit operator Result<T, TE>(T value) => new Result<T, TE>(value);
    
    public static implicit operator Result<T, TE>(TE error) => new Result<T, TE>(false, default, error);

    // public override bool Equals(object? obj)
    // {
    //     return ReferenceEquals(this, obj);
    // }
    
    // public override int GetHashCode()
    // {
    //     return base.GetHashCode();
    // }
    
    public override string ToString()
    {
        return IsOk ? $"Ok({Value})" : $"Error({Error})";
    }
    
}


public static class ResultExtensions
{
    
    // map ok and map err
    public static Result<TB, TE> MapOk<T, TB, TE>(this IResult<T, TE> result, Func<T, TB> func)
    {
        return result.IsOk ? Result<TB, TE>.Ok(func(result.Value!)) : Result<TB, TE>.Err(result.Error!);
    }
    
    public static async ValueTask<Result<TB, TE>> MapOkAsync<T, TB, TE>(this IResult<T, TE> result, Func<T, ValueTask<TB>> func)
    {
        return result.IsOk ? Result<TB, TE>.Ok(await func(result.Value!)) : Result<TB, TE>.Err(result.Error!);
    }
    public static async ValueTask<Result<TB, TE>> MapOkAsync<T, TB, TE>(this ValueTask<IResult<T, TE>> result, Func<T, ValueTask<TB>> func)
    {
        var r = await result;
        return r.IsOk ? Result<TB, TE>.Ok(await func(r.Value!)) : Result<TB, TE>.Err(r.Error!);
    }
    

    
    public static Result<T, TEB> MapErr<T, TE, TEB>(this IResult<T, TE> result, Func<TE, TEB> func)
    {
        return result.IsOk ? Result<T, TEB>.Ok(result.Value!) : Result<T, TEB>.Err(func(result.Error!));
    }
    
    // flatten nested
    
    public static IResult<T, TE> Flatten<T, TE>(this IResult<IResult<T, TE>, TE> result)
    {
        return result.IsOk ? result.Value! : Result<T, TE>.Err(result.Error!);
    }
    
}

public class CodeResult<T> : Result<T, HttpStatus>
{

    protected CodeResult(T okValue) : base(okValue)
    {
    }

    protected CodeResult(bool ignored, HttpStatus errorValue) : base((bool)false, errorValue)
    {
    }
    
    public new static CodeResult<T> Err(HttpStatus errorValue) => new CodeResult<T>(false, errorValue);
    public static CodeResult<T> ErrFrom(HttpStatusCode errorValue, string sourceAction) => 
        new CodeResult<T>(false, new HttpStatus((int)errorValue).WithAddFrom(sourceAction));

    public static CodeResult<T> ErrFrom(HttpStatus status, string sourceAction) => 
        new CodeResult<T>(false, status.WithAddFrom(sourceAction));

    public new static CodeResult<T> Ok(T okValue) => new CodeResult<T>(okValue);

    public static CodeResult<T> Ok<TA>(TA okValue) where TA : T => new CodeResult<T>(okValue);

    public static implicit operator CodeResult<T>(T value) => new CodeResult<T>(value);
    
    public static implicit operator CodeResult<T>(HttpStatus error) => new CodeResult<T>(false, error);
    
    public override string ToString()
    {
        return IsOk ? $"Ok({Value})" : $"Err({Error})";
    }

    public CodeResult<TE> MapOk<TE>(Func<T, TE> func)
    {
        return IsOk ? CodeResult<TE>.Ok(func(Value!)) : CodeResult<TE>.Err(Error!);
    }
    
    public async ValueTask<CodeResult<TE>> MapOkAsync<TE>(Func<T, ValueTask<TE>> func)
    {
        return IsOk ? CodeResult<TE>.Ok(await func(Value!)) : CodeResult<TE>.Err(Error!);
    }
    
}

public class CodeResult : Result<HttpStatus, HttpStatus>
{

    protected CodeResult(HttpStatus okValue) : base(okValue)
    {
        
    }

    protected CodeResult(bool ignored, HttpStatus errorValue) : base(ignored, errorValue)
    {
    }
    public new static CodeResult Err(HttpStatus errorValue) => new CodeResult(false, errorValue);
    
    public static CodeResult Err(HttpStatusCode errorValue) => new CodeResult(false, errorValue);
    public static CodeResult ErrFrom(HttpStatusCode errorValue, string sourceAction) => 
        new CodeResult(false, new HttpStatus((int)errorValue).WithAddFrom(sourceAction));

    public static CodeResult ErrFrom(HttpStatus status, string sourceAction) => 
        new CodeResult(false, status.WithAddFrom(sourceAction));

    public new static CodeResult Ok(HttpStatus okValue) => new CodeResult(okValue);
    
    public new static CodeResult Ok() => new CodeResult(HttpStatus.Ok);

    public static CodeResult FromException(Exception exception, string? sourceAction)
    {
        return exception switch
        {
            UnauthorizedAccessException e => Err(HttpStatus.Forbidden.WithAppend(e.Message).WithAddFrom(sourceAction)),
            FileNotFoundException a => Err(HttpStatus.NotFound.WithAppend(a.Message).WithAddFrom(sourceAction)),
            DirectoryNotFoundException b => Err(HttpStatus.NotFound.WithAppend(b.Message).WithAddFrom(sourceAction)),
            BlobMissingException c => Err(HttpStatus.NotFound.WithAppend(c.Message).WithAddFrom(sourceAction)),
            _ => Err(HttpStatus.ServerError.WithAppend(exception.Message).WithAddFrom(sourceAction))
        };
    }
    
    public static CodeResult FromException(Exception exception) => FromException(exception, null);
    
    public override string ToString()
    {
        return IsOk ? $"Ok({Value})" : $"Err({Error})";
    }
}


[StructLayout(LayoutKind.Explicit)]
public readonly struct ResultValue<T, TE> : IResult<T, TE>
{
    
    private ResultValue(T value)
    {
        _ok = true;
        _value = value;
    }
    private ResultValue(bool ignored, TE error)
    {
        _ok = false;
        _error = error;
    }
    [FieldOffset(0)] private readonly bool _ok;
    [FieldOffset(1)] private readonly TE? _error;
    [FieldOffset(2)] private readonly T? _value;
    
    public bool IsOk => _ok;
    public bool IsError => !_ok;
    public T? Value => _value;
    public TE? Error => _error;
    
    public static implicit operator ResultValue<T, TE>(T value) => new ResultValue<T, TE>(value);
    
    public static implicit operator ResultValue<T, TE>(TE error) => new ResultValue<T, TE>(false, error);
    
    public static ResultValue<T, TE> Ok(T value) => new ResultValue<T, TE>(value);
    
    public static ResultValue<T, TE> Err(TE error) => new ResultValue<T, TE>(false, error);
    
}


public class DisposableResult<T, TE> : Result<T, TE>, IDisposableResult<T, TE> 
{

    protected DisposableResult(T okValue, bool disposeValue) : base(okValue)
    {
        DisposeValue = disposeValue;
    }

    protected DisposableResult(bool ignored, TE errorValue, bool disposeError) : base(false, default(T), errorValue)
    {
        DisposeError = disposeError;
    }
    
    private bool DisposeValue { get; }
    private bool DisposeError { get; }
    public void Dispose()
    {
        try
        {
            if (DisposeValue && Value is IDisposable d) d.Dispose();
        }
        finally
        {
            if (DisposeError && Error is IDisposable e) e.Dispose();
        }
    }
    
    public static DisposableResult<T, TE> Err(TE errorValue, bool disposeError) => new DisposableResult<T, TE>(false, errorValue, disposeError);

    public static DisposableResult<T, TE> Ok(T okValue, bool disposeValue) => new DisposableResult<T, TE>(okValue, disposeValue);

}