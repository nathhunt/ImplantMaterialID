using System;
using ImplantMaterialID.Models;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class PercentileTests
    {
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(1.0)]
        public void SingleValue_IsReturnedForAnyFraction(double fraction)
        {
            Assert.Equal(42.0, Percentile.Linear(new[] { 42.0 }, fraction));
        }

        [Fact]
        public void ZeroAndOne_GiveMinimumAndMaximum()
        {
            var sample = new[] { -3.0, 1.0, 7.0, 20.0 };

            Assert.Equal(-3.0, Percentile.Linear(sample, 0.0));
            Assert.Equal(20.0, Percentile.Linear(sample, 1.0));
        }

        // Expected values are what numpy.percentile (default "linear") and Excel PERCENTILE.INC
        // give for the same input - Hyndman & Fan definition 7, position (n - 1) * p.
        [Theory]
        [InlineData(new[] { 1.0, 2.0, 3.0, 4.0 }, 0.9, 3.7)]
        [InlineData(new[] { 1.0, 2.0, 3.0, 4.0 }, 0.5, 2.5)]
        [InlineData(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 }, 0.9, 9.1)]
        [InlineData(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 }, 0.7, 7.3)]
        [InlineData(new[] { 100.0, 200.0, 1000.0 }, 0.8, 680.0)]
        public void MatchesDefinition7(double[] sample, double fraction, double expected)
        {
            Assert.Equal(expected, Percentile.Linear(sample, fraction), 9);
        }

        [Fact]
        public void ExactlyOnASampleValue_IsNotInterpolated()
        {
            // (5 - 1) * 0.75 = 3 exactly.
            Assert.Equal(40.0, Percentile.Linear(new[] { 10.0, 20.0, 30.0, 40.0, 50.0 }, 0.75));
        }

        [Fact]
        public void NullSample_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Percentile.Linear(null, 0.5));
        }

        [Fact]
        public void EmptySample_Throws()
        {
            Assert.Throws<ArgumentException>(() => Percentile.Linear(new double[0], 0.5));
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.01)]
        public void FractionOutsideZeroToOne_Throws(double fraction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Percentile.Linear(new[] { 1.0, 2.0 }, fraction));
        }
    }
}
