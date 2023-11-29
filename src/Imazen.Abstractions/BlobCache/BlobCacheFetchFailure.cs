using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.BlobCache;

public interface IBlobCacheFetchFailure
{
    IBlobCache? NotifyOfResult { get; }
    IBlobCache? NotifyOfExternalHit { get; }
    HttpStatus Status { get; }
    

}
public record BlobCacheFetchFailure : IBlobCacheFetchFailure
{
    public IBlobCache? NotifyOfResult { get; init; }
    public IBlobCache? NotifyOfExternalHit { get; init;}
    public HttpStatus Status { get; init;}
    

    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> ErrorResult(HttpStatus status, IBlobCache? notifyOfResult, IBlobCache? notifyOfExternalHit) => Result<BlobWrapper, IBlobCacheFetchFailure>.Err(new BlobCacheFetchFailure {Status = status, NotifyOfResult = notifyOfResult, NotifyOfExternalHit = notifyOfExternalHit});
    
    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> ErrorResult(HttpStatus status) => Result<BlobWrapper, IBlobCacheFetchFailure>.Err(new BlobCacheFetchFailure {Status = status});
    
    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> MissResult() => Result<BlobWrapper, IBlobCacheFetchFailure>.Err(new BlobCacheFetchFailure {Status = HttpStatus.NotFound});
    
    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> MissResult(IBlobCache? notifyOfResult, IBlobCache? notifyOfExternalHit) => Result<BlobWrapper, IBlobCacheFetchFailure>.Err(new BlobCacheFetchFailure {Status = HttpStatus.NotFound, NotifyOfResult = notifyOfResult, NotifyOfExternalHit = notifyOfExternalHit});
    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> OkResult(BlobWrapper blob) => Result<BlobWrapper, IBlobCacheFetchFailure>.Ok(blob);
    public static IResult<IBlobWrapper, IBlobCacheFetchFailure> OkResult(IBlobWrapper blob) => Result<IBlobWrapper, IBlobCacheFetchFailure>.Ok(blob);

}