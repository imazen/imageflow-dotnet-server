namespace Imazen.Abstractions.Resulting;

public class ResultIsOkException<T> : Exception
{
    public ResultIsOkException(T? okValue, string? message = null) : base(message ?? "Result does not contain the expected error value")
    {
        OkValue = okValue;
    }
    
    public T? OkValue { get; }

    
    public override string ToString()
    {
        return $"{base.ToString()}: {OkValue}";
    }
    
}