using System.Collections;

namespace Imazen.Abstractions.Resulting;

public class ResultIsErrorException<TE> : Exception
{
    public ResultIsErrorException(TE? errorValue, string? message = null) : base(message ?? "Result does not contain expected OK value")
    {
        ErrorValue = errorValue;
    }
    
    public TE? ErrorValue { get; }

    public override IDictionary Data =>ErrorValue == null ? base.Data : new Dictionary<object, object>
    {
         {"ErrorValue", ErrorValue}
    };
    
    public override string ToString()
    {
        return $"{base.ToString()}: {ErrorValue}";
    }
    
}