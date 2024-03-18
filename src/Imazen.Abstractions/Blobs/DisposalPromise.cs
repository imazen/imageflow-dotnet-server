namespace Imazen.Abstractions.Blobs;

public enum DisposalPromise
{
    CallerDisposesStreamThenBlob = 1,
    CallerDisposesBlobOnly = 2
}