global using IBlobResult = Imazen.Abstractions.Resulting.IDisposableResult<Imazen.Abstractions.Blobs.IBlobWrapper, Imazen.Abstractions.Resulting.HttpStatus>;

using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.Blobs;


public class BlobResult : DisposableResult<IBlobWrapper, HttpStatus>
{
    private BlobResult(IBlobWrapper okValue, bool disposeValue) : base(okValue, disposeValue)
    {
    }

    private BlobResult(bool ignored, HttpStatus errorValue, bool disposeError) : base(false, errorValue, disposeError)
    {
    }
    
    public new static BlobResult Ok(IBlobWrapper okValue, bool disposeValue) => new BlobResult(okValue, disposeValue);

    public new static BlobResult Err(HttpStatus errorValue, bool disposeError) => new BlobResult(false, errorValue, disposeError);
}