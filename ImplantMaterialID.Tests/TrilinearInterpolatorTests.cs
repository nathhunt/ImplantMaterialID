using System;
using ImplantMaterialID.Models;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class TrilinearInterpolatorTests
    {
        // Irregularly spaced axes, like the real reference tables.
        private static readonly double[] Xs = { 4, 6, 10, 16 };
        private static readonly double[] Ys = { 0.6, 0.8, 1.4 };
        private static readonly double[] Zs = { 1000, 1500, 2000 };

        // Trilinear interpolation reproduces any function of the form
        // a + bx + cy + dz + exy + fxz + gyz + hxyz exactly, so it makes a good oracle.
        private static double Multilinear(double x, double y, double z) =>
            5 + 2 * x - 3 * y + 0.01 * z + 0.5 * x * y - 0.002 * x * z + 0.004 * y * z + 0.0001 * x * y * z;

        private static double[,,] BuildGrid(Func<double, double, double, double> f)
        {
            var grid = new double[Xs.Length, Ys.Length, Zs.Length];
            for (int i = 0; i < Xs.Length; i++)
                for (int j = 0; j < Ys.Length; j++)
                    for (int k = 0; k < Zs.Length; k++)
                        grid[i, j, k] = f(Xs[i], Ys[j], Zs[k]);
            return grid;
        }

        [Fact]
        public void GridPoints_AreReproducedExactly()
        {
            var grid = BuildGrid((x, y, z) => x * 1000 + y * 10 + z);

            for (int i = 0; i < Xs.Length; i++)
                for (int j = 0; j < Ys.Length; j++)
                    for (int k = 0; k < Zs.Length; k++)
                    {
                        var r = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, Xs[i], Ys[j], Zs[k]);
                        Assert.Equal(grid[i, j, k], r.Value);
                        Assert.False(r.XClamped || r.YClamped || r.ZClamped);
                    }
        }

        [Theory]
        [InlineData(5.0, 0.7, 1250.0)]
        [InlineData(7.3, 1.1, 1999.0)]
        [InlineData(15.9, 0.61, 1001.0)]
        [InlineData(4.0, 1.4, 1700.0)]
        public void BetweenGridPoints_ReproducesMultilinearFunction(double x, double y, double z)
        {
            var grid = BuildGrid(Multilinear);

            var r = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, x, y, z);

            Assert.Equal(Multilinear(x, y, z), r.Value, 9);
            Assert.False(r.XClamped || r.YClamped || r.ZClamped);
        }

        [Fact]
        public void CentreOfCell_IsAverageOfItsEightCorners()
        {
            var grid = new double[2, 2, 2] { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 100 } } };

            var r = TrilinearInterpolator.Interpolate(new[] { 0.0, 1.0 }, new[] { 0.0, 1.0 }, new[] { 0.0, 1.0 }, grid, 0.5, 0.5, 0.5);

            Assert.Equal((1 + 2 + 3 + 4 + 5 + 6 + 7 + 100) / 8.0, r.Value, 12);
        }

        [Fact]
        public void BelowRange_IsClampedToFirstGridValue()
        {
            var grid = BuildGrid(Multilinear);

            var r = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, 2.0, 0.8, 1500);

            Assert.Equal(Multilinear(4, 0.8, 1500), r.Value, 9);
            Assert.True(r.XClamped);
            Assert.False(r.YClamped);
            Assert.False(r.ZClamped);
        }

        [Fact]
        public void AboveRange_IsClampedToLastGridValue()
        {
            var grid = BuildGrid(Multilinear);

            var r = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, 10, 2.0, 2500);

            Assert.Equal(Multilinear(10, 1.4, 2000), r.Value, 9);
            Assert.False(r.XClamped);
            Assert.True(r.YClamped);
            Assert.True(r.ZClamped);
        }

        [Fact]
        public void ExactlyOnTheEdges_IsNotFlaggedAsClamped()
        {
            var grid = BuildGrid(Multilinear);

            var low = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, 4, 0.6, 1000);
            var high = TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, 16, 1.4, 2000);

            Assert.False(low.XClamped || low.YClamped || low.ZClamped);
            Assert.False(high.XClamped || high.YClamped || high.ZClamped);
        }

        [Fact]
        public void SingleValueAxis_IsClampedOnlyWhenQueryDiffers()
        {
            var grid = new double[1, 1, 1] { { { 7.0 } } };
            var axis = new[] { 1.0 };

            var hit = TrilinearInterpolator.Interpolate(axis, axis, axis, grid, 1.0, 1.0, 1.0);
            var miss = TrilinearInterpolator.Interpolate(axis, axis, axis, grid, 2.0, 1.0, 0.5);

            Assert.Equal(7.0, hit.Value);
            Assert.False(hit.XClamped || hit.YClamped || hit.ZClamped);
            Assert.Equal(7.0, miss.Value);
            Assert.True(miss.XClamped);
            Assert.False(miss.YClamped);
            Assert.True(miss.ZClamped);
        }

        [Fact]
        public void NullArguments_Throw()
        {
            var grid = BuildGrid(Multilinear);

            Assert.Throws<ArgumentNullException>(() => TrilinearInterpolator.Interpolate(null, Ys, Zs, grid, 5, 1, 1500));
            Assert.Throws<ArgumentNullException>(() => TrilinearInterpolator.Interpolate(Xs, Ys, Zs, null, 5, 1, 1500));
        }

        [Fact]
        public void GridShapeNotMatchingAxes_Throws()
        {
            var grid = new double[Xs.Length, Ys.Length, Zs.Length - 1];

            Assert.Throws<ArgumentException>(() => TrilinearInterpolator.Interpolate(Xs, Ys, Zs, grid, 5, 1, 1500));
        }
    }
}
