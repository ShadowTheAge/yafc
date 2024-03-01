using System.Reflection;
using Xunit;

namespace YAFC.Model.Tests {
    public class BitsTests {
        [Fact]
        public void New_WhenTrueIsProvided_ShouldHaveBit0Set() {
            Bits bits = new Bits(true);

            Assert.True(bits[0]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(33)]
        [InlineData(75)]
        public void SetBit_WhenGivenABit_ShouldReturnSetBit(int bit) {
            Bits bits = new Bits();

            bits[bit] = true;

            Assert.True(bits[bit], "Set bit should be set");
            for (int i = 0; i <= 128; i++) {
                if (i != bit)
                    Assert.False(bits[i], "Other bits should be clear");
            }
        }

        [Fact]
        public void IsClear_WhenNotBitsAreSet_ShouldReturnTrue() {
            Bits bits = new Bits();

            Assert.True(bits.IsClear(), "IsClear() should return true, as no bits are set");
        }

        [Fact]
        public void IsClear_WhenABitSet_ShouldReturnFalse() {
            Bits bits = new Bits();

            bits[2] = true;

            Assert.False(bits.IsClear(), "IsClear() should return false, as bit 2 is set");
        }

        [Theory]
        [InlineData(new int[] { })]
        [InlineData(new int[] { 0 })]
        [InlineData(new int[] { 1 })]
        [InlineData(new int[] { 1, 10 })]
        [InlineData(new int[] { 1, 76, 42, 3, 11, 68 })]
        public void HighestBitSet_WithGivenListOfBits_ShouldReturnHighestBit(int[] bitsToSet) {
            Bits bits = new Bits();
            int highestBit = -1;

            foreach (int bit in bitsToSet) {
                if (bit > highestBit)
                    highestBit = bit;
                bits[bit] = true;
            }

            Assert.Equal(highestBit, bits.HighestBitSet());
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(24, 42)]
        public void AndOperator_WithGivenInputBits_ShouldReturnANDedBits(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsData.SetValue(b, new ulong[] { bValue });
            bitsLength.SetValue(a, 8);
            bitsLength.SetValue(b, 8);

            var result = a & b;

            ulong resultValue = ((ulong[])bitsData.GetValue(result))[0];

            Assert.Equal(aValue & bValue, resultValue);
        }

        [Fact]
        public void AndOperator_WithOneLargeValue_ReturnCorrectBits() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 128);

            var result = a & b;

            ulong[] resultValue = (ulong[])bitsData.GetValue(result);

            Assert.Equal((ulong)4 & 4, resultValue[0]);
            Assert.Equal(0ul, resultValue[1]);
        }

        [Fact]
        public void AndOperator_WithLargeValues_ReturnCorrectBits() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4, 1 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 128);

            var result = a & b;

            ulong[] resultValue = (ulong[])bitsData.GetValue(result);

            Assert.Equal((ulong)4 & 4, resultValue[0]);
            Assert.Equal((ulong)3 & 1, resultValue[1]);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(24, 42)]
        public void OrOperator_WithGivenInputBits_ShouldReturnORedBits(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsData.SetValue(b, new ulong[] { bValue });
            bitsLength.SetValue(a, 8);
            bitsLength.SetValue(b, 8);

            var result = a | b;

            ulong resultValue = ((ulong[])bitsData.GetValue(result))[0];

            Assert.Equal(aValue | bValue, resultValue);
        }

        [Fact]
        public void OrOperator_WithOneLargeValue_ReturnCorrectBits() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 128);

            var result = a | b;

            ulong[] resultValue = (ulong[])bitsData.GetValue(result);

            Assert.Equal((ulong)4 | 4, resultValue[0]);
            Assert.Equal(3ul, resultValue[1]);
        }

        [Fact]
        public void OrOperator_WithLargeValues_ReturnCorrectBits() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4, 1 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 128);

            var result = a | b;

            ulong[] resultValue = (ulong[])bitsData.GetValue(result);

            Assert.Equal((ulong)4 | 4, resultValue[0]);
            Assert.Equal((ulong)3 | 1, resultValue[1]);
        }

        [Theory]
        [InlineData(0, 0 << 1)]
        [InlineData(1, 1 << 1)]
        [InlineData(24, 24 << 1)]
        public void ShiftLeftOperator_WithGivenInputBits_ShouldReturnShiftedBits(ulong aValue, ulong expectedValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsLength.SetValue(a, 8);

            var result = a << 1;

            ulong resultValue = ((ulong[])bitsData.GetValue(result))[0];

            Assert.Equal(expectedValue, resultValue);
        }

        [Theory]
        [InlineData(1, 1)] // only subtracting by 1 is supported
        [InlineData(42, 1)]
        public void SubtractOperator_WithGivenInputBits_ShouldReturnCorrectResult(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsLength.SetValue(a, 8);

            var result = a - bValue;

            ulong resultValue = ((ulong[])bitsData.GetValue(result))[0];

            Assert.Equal(aValue - bValue, resultValue);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        public void EqualOperator_WithGivenInputBits_ShouldReturnCorrectResult(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsLength.SetValue(a, 8);

            bool result = a == bValue;

            Assert.Equal(aValue == bValue, result);
        }

        [Fact]

        public void EqualOperator_WithLargeValues_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsLength.SetValue(a, 128);

            bool result = a == 4;

            // First data element matches, but next one is not zero
            Assert.False(result);

            bitsData.SetValue(a, new ulong[] { 4, 0 });

            bool result2 = a == 4;

            // First data element matches and rest is cleared (zero)
            Assert.True(result2);
        }

        [Fact]
        public void EqualOperator_WithSameValueDifferentLengths_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4, 3 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 126); // drop some zeros

            Assert.True(a == b);
        }

        [Fact]
        public void EqualOperator_WithNull_ShouldReturnFalse() {
            Bits a = null;
            bool result = a == 0;

            Assert.False(result);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        public void UnequalOperator_WithGivenInputBits_ShouldReturnCorrectResult(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsLength.SetValue(a, 8);

            bool result = a != bValue;

            Assert.Equal(aValue != bValue, result);
        }

        [Fact]
        public void UnequalOperator_WithLargeValues_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsLength.SetValue(a, 128);

            bool result = a != 4;

            // First data element matches, but next one is not zero so unequal
            Assert.True(result);

            bitsData.SetValue(a, new ulong[] { 4, 0 });

            bool result2 = a != 4;

            Assert.False(result2);
        }

        [Fact]
        public void UnequalOperator_WithSameValueDifferentLengths_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 4, 3 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 126); // drop some zeros

            // Number of bits does not matter, so a == b
            Assert.False(a != b);
        }


        [Fact]
        public void UnequalOperator_WithNull_ShouldReturnFalse() {
            Bits a = null;
            bool result = a != 0;

            Assert.False(result);
        }
        [Fact]
        public void SubtractOperator_WithLongInputBits_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            bitsData.SetValue(a, new ulong[] { 0, 3 });
            bitsLength.SetValue(a, 128);

            var result = a - 1;


            ulong[] resultValue = (ulong[])bitsData.GetValue(result);

            Assert.Equal(~0ul, resultValue[0]);
            Assert.Equal(2ul, resultValue[1]);
        }


        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        public void LesserThanOperator_WithGivenInputBits_ShouldReturnCorrectResult(ulong aValue, ulong bValue) {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { aValue });
            bitsData.SetValue(b, new ulong[] { bValue });
            bitsLength.SetValue(a, 8);
            bitsLength.SetValue(b, 8);

            bool result = a < b;

            Assert.Equal(aValue < bValue, result);
        }

        [Fact]

        public void LesserThanOperator_WithLargeValues_ShouldReturnCorrectResult() {
            var bitsType = typeof(Bits);
            var bitsData = bitsType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bitsLength = bitsType.GetField("_length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Bits a = new Bits();
            Bits b = new Bits();
            bitsData.SetValue(a, new ulong[] { 4, 3 });
            bitsData.SetValue(b, new ulong[] { 3, 3 });
            bitsLength.SetValue(a, 128);
            bitsLength.SetValue(b, 128);

            bool result = a < b;

            // Second data element matches, but first one is not lesser
            Assert.False(result);

            bitsData.SetValue(a, new ulong[] { 2, 3 });

            bool result2 = a < b;

            // Second data element matches, but first one is lesser
            Assert.True(result2);
        }
    }
}
