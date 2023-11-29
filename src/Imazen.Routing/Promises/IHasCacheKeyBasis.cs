using System.Buffers;

namespace Imazen.Routing.Promises;

public interface IHasCacheKeyBasis
{
    void WriteCacheKeyBasisPairsTo(IBufferWriter<byte> writer);
}