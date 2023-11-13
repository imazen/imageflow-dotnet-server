using System;
using Xunit;
using Imazen.Common.Instrumentation.Support;

namespace Imazen.Common.Tests
{
    public class ConcurrentBitArrayTests
    {

        [Fact]
        public void SetBitTrue_WhenIndexIsNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => bitArray[-1] = true);
        }

        [Fact]
        public void SetBitTrue_WhenIndexIsEqualToBitCount_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => bitArray[16_000_000] = true);
        }

        [Fact]
        public void SetBitTrue_WhenIndexIsGreaterThanBitCount_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => bitArray[16_000_001] = true);
        }


        [Fact]
        public void TestSetBitTrue()
        {
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);
            bitArray[1] = true;
            bitArray[3] = true;
            Assert.Equal(new byte[] { 0, 10, 0, 0, 0, 0, 0, 0 }, bitArray.ToBytes());
        }

        [Fact]
        public void TestMergeTrueBitsFrom()
        {
            ConcurrentBitArray bitArray1 = new ConcurrentBitArray(16_000_000);
            ConcurrentBitArray bitArray2 = new ConcurrentBitArray(16_000_000);
            bitArray1[1] = true;
            bitArray1[3] = true;
            bitArray2[2] = true;
            bitArray2[3] = true;
            bitArray1.MergeTrueBitsFrom(bitArray2);
            Assert.Equal(new byte[] { 0, 14, 0, 0, 0, 0, 0, 0 }, bitArray1.ToBytes());
        }

        [Fact]
        public void TestLoadFromBytes()
        {
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);
            bitArray.LoadFromBytes(new byte[] { 0, 14, 0, 0, 0, 0, 0, 0 });
            Assert.Equal(new byte[] { 0, 14, 0, 0, 0, 0, 0, 0 }, bitArray.ToBytes());
        }

        [Fact]
        public void TestToBytes()
        {
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);
            bitArray[1] = true;
            bitArray[3] = true;
            Assert.Equal(new byte[] { 0, 10, 0, 0, 0, 0, 0, 0 }, bitArray.ToBytes());
        }

        [Fact]
        public void TestGetBitTrue()
        {
            ConcurrentBitArray bitArray = new ConcurrentBitArray(16_000_000);
            bitArray[1] = true;
            bitArray[3] = true;
            Assert.True(bitArray[1]);
            Assert.True(bitArray[3]);
            Assert.False(bitArray[2]);
        }


        [Fact]
        public void MergeTrueBitsFrom_ThrowsException_WhenBitArraySizesDoNotMatch2()
        {
            // Arrange
            ConcurrentBitArray bitArray1 = new ConcurrentBitArray(16_000_001);
            ConcurrentBitArray bitArray2 = new ConcurrentBitArray(16_000_000);

            // Act
            Action action = () => bitArray1.MergeTrueBitsFrom(bitArray2);

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void MergeTrueBitsFrom_ThrowsException_WhenBitArraySizesDoNotMatch3()
        {
            // Arrange
            ConcurrentBitArray bitArray1 = new ConcurrentBitArray(16_000_001);
            ConcurrentBitArray bitArray2 = new ConcurrentBitArray(16_000_002);

            // Act
            Action action = () => bitArray1.MergeTrueBitsFrom(bitArray2);

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void TestSerialization()
        {
            // Arrange
            ConcurrentBitArray bitArray1 = new ConcurrentBitArray(16_000_000);
            ConcurrentBitArray bitArray2 = new ConcurrentBitArray(16_000_000);

            // Act
            bitArray1[1] = true;
            bitArray1[3] = true;
            bitArray2[2] = true;
            bitArray2[3] = true;

            byte[] serializedData1 = bitArray1.ToBytes();
            ConcurrentBitArray bitArray3 = new ConcurrentBitArray(16_000_000);
            bitArray3.LoadFromBytes(serializedData1);
            bitArray3.MergeTrueBitsFrom(bitArray2);

            // Assert
            byte[] serializedData3 = bitArray3.ToBytes();
            ConcurrentBitArray bitArray4 = new ConcurrentBitArray(16_000_000);
            bitArray4.LoadFromBytes(serializedData3);

            byte[] finalSerializedData = bitArray4.ToBytes();
            for (int i = 0; i < serializedData3.Length; i++)
            {
                Assert.Equal(serializedData3[i], finalSerializedData[i]);
            }
        }
    }
}