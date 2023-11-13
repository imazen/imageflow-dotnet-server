using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace Imazen.Common.Instrumentation.Support
{
    internal class ConcurrentBitArray
    {
        private readonly long[] _data;
        private readonly int _bitCount;

        public int BitCount => _bitCount;

        public int ByteCount => _data.Length / 8;
        public ConcurrentBitArray(int bitCount)
        {
            // must be a multiple of 8
            if (bitCount % 8 != 0)
            {
                throw new ArgumentException("ConcurrentBitArray requires the bit count to be a multiple of 8");
            }
            //must be a platform in little endian mode
            if (!BitConverter.IsLittleEndian)
            {
                throw new ArgumentException("ConcurrentBitArray only supports little endian platforms");
            }
            _bitCount = bitCount;
            _data = new long[(bitCount + 63) / 64];
        }

        public void LoadFromBytes(byte[] bytes)
        {

            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (bytes.Length != ByteCount)
            {
                throw new ArgumentException($"Invalid input byte array size - must match the number of bytes in the ConcurrentBitArray {bytes.Length} != {ByteCount}");
            }
            Buffer.BlockCopy(bytes, 0, _data, 0, bytes.Length);
        }

        public void LoadFromSpan(Span<byte> bytes)
        {
            if (bytes.Length != ByteCount)
            {
                throw new ArgumentException($"Invalid input span size - must match the number of bytes in the ConcurrentBitArray {bytes.Length} != {ByteCount}");
            }
            MemoryMarshal.Cast<byte, long>(bytes).CopyTo(_data);
        }


        public byte[] ToBytes()
        {
            byte[] bytes = new byte[_data.Length * 8];
            Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);
            return bytes;
        }



        public void MergeTrueBitsFrom(ConcurrentBitArray other)
        {
            // must be the same size
            if (other._bitCount != _bitCount)
            {
                throw new ArgumentException("ConcurrentBitArray instances must be the same size");
            }
            for (int i = 0; i < _data.Length; i++)
            {
                long otherValue = other._data[i];
                long oldValue, newValue;
                if (otherValue == 0)
                {
                    continue; // nothing to do
                }
                do
                {
                    oldValue = _data[i];
                    newValue = oldValue | otherValue;
                } while (Interlocked.CompareExchange(ref _data[i], newValue, oldValue) != oldValue);
            }
        }

        
        public bool GetBit(int index)
        {
            if (index < 0 || index >= _bitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_bitCount - 1}");
            }
            int arrayIndex = index / 64;
            // Reverse for little endian so the raw bytes make sense.
            long bitMask = 1L << (63 - (index % 64));
            return (_data[arrayIndex] & bitMask) != 0;
        }


        internal void SetBit(int index, bool value)
        {
            if (index < 0 || index >= _bitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_bitCount - 1}");
            }
            int arrayIndex = index / 64;
            long bitMask = 1L << (63 - (index % 64));
            long oldValue, newValue;
            do
            {
                oldValue = _data[arrayIndex];
                newValue = value ? oldValue | bitMask : oldValue & ~bitMask;
            } while (Interlocked.CompareExchange(ref _data[arrayIndex], newValue, oldValue) != oldValue);


        }

        
        public bool this[int index]
        {
            get { return GetBit(index); }
            set { SetBit(index, value); }
        }
    }
}
