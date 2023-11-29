using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Imazen.Abstractions.Resulting;

public static class ExtensionMethods
{
    public static void Unwrap(this IResult result)
    {
        if (result.IsError)
        {
            throw new ResultIsErrorException<string>(null);
        }
    }
    public static T Unwrap<T,TE>(this IResult<T,TE> result)
    {
        if (result.IsError)
        {
            if (result.Error is Exception e) throw e;
            throw new ResultIsErrorException<TE>(result.Error);
        }
        return result.Value!;
    }
    
    public static bool TryUnwrap<T,TE>(this IResult<T,TE> result, [MaybeNullWhen(false)] out T value)
    {
        if (result.IsError)
        {
            value = default;
            return false;
        }
        value = result.Value!;
        return true;
    }
        
    public static T Unwrap<T>(this CodeResult<T> result)
    {
        if (result.IsError)
        {
            throw new ResultIsErrorException<string>(null);
        }
        return result.Value!;
    }
    public static bool TryUnwrap<T>(this CodeResult<T> result, [MaybeNullWhen(false)] out T value)
    {
        if (result.IsError)
        {
            value = default;
            return false;
        }
        value = result.Value!;
        return true;
    }
    
    public static TE UnwrapError<T,TE>(this IResult<T,TE> result)
    {
        if (result.IsOk) throw new ResultIsOkException<T>(result.Value);
        return result.Error!;
    }
    
    public static bool TryUnwrapError<T,TE>(this IResult<T,TE> result, [MaybeNullWhen(false)] out TE error)
    {
        if (result.IsOk)
        {
            error = default;
            return false;
        }
        error = result.Error!;
        return true;
    }
    
    public static void UnwrapError(this IResult result)
    {
        if (result.IsOk)
        {
            throw new ResultIsOkException<string>(null);
        }
    }
    
    
    // Extension methods LogAsError, LogAsWarning, LogAsInfo, LogAsDebug, LogAsTrace
    

    public static void LogAsError<T,TE>(this ILogger logger, Result<T, TE> result)
    {
        if (result.IsError)
        {
            logger.LogError(result.ToString());
        }
    }
    public static void LogAsWarning<T,TE>(this ILogger logger, Result<T, TE> result)
    {
        if (result.IsError)
        {
            logger.LogWarning(result.ToString());
        }
    }

}