using System;

namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Result of interpolating a single 3-D table at one (x, y, z) query point.
    /// </summary>
    public struct InterpolationResult
    {
        public double Value;

        /// <summary>True if x fell outside the table's range and was clamped to the nearest edge.</summary>
        public bool XClamped;

        /// <summary>True if y fell outside the table's range and was clamped to the nearest edge.</summary>
        public bool YClamped;

        /// <summary>True if z fell outside the table's range and was clamped to the nearest edge.</summary>
        public bool ZClamped;
    }

    /// <summary>
    /// Trilinear interpolation over an irregularly-spaced-but-monotonic 3-D grid, implemented as
    /// a weighted sum of the 8 surrounding grid points, so an exact hit on a grid point
    /// reproduces the table value exactly. Query points outside the table are clamped to the
    /// nearest edge rather than extrapolated, since linear extrapolation of a CT-number-vs-size
    /// curve is not reliable outside the characterised range.
    /// </summary>
    public static class TrilinearInterpolator
    {
        /// <param name="xValues">Ascending grid coordinates for dimension 0, e.g. diameters.</param>
        /// <param name="yValues">Ascending grid coordinates for dimension 1, e.g. pixel sizes.</param>
        /// <param name="zValues">Ascending grid coordinates for dimension 2, e.g. thresholds.</param>
        /// <param name="grid">grid[i, j, k] = table value at (xValues[i], yValues[j], zValues[k]).</param>
        public static InterpolationResult Interpolate(
            double[] xValues, double[] yValues, double[] zValues, double[,,] grid,
            double x, double y, double z)
        {
            if (xValues == null || yValues == null || zValues == null || grid == null)
                throw new ArgumentNullException("Reference table arrays must not be null.");
            if (grid.GetLength(0) != xValues.Length || grid.GetLength(1) != yValues.Length || grid.GetLength(2) != zValues.Length)
                throw new ArgumentException("Grid dimensions do not match the supplied axis arrays.");

            var (xi0, xi1, xt, xClamped) = FindBracket(xValues, x);
            var (yi0, yi1, yt, yClamped) = FindBracket(yValues, y);
            var (zi0, zi1, zt, zClamped) = FindBracket(zValues, z);

            double value =
                (1 - xt) * (1 - yt) * (1 - zt) * grid[xi0, yi0, zi0] +
                xt * (1 - yt) * (1 - zt) * grid[xi1, yi0, zi0] +
                (1 - xt) * yt * (1 - zt) * grid[xi0, yi1, zi0] +
                xt * yt * (1 - zt) * grid[xi1, yi1, zi0] +
                (1 - xt) * (1 - yt) * zt * grid[xi0, yi0, zi1] +
                xt * (1 - yt) * zt * grid[xi1, yi0, zi1] +
                (1 - xt) * yt * zt * grid[xi0, yi1, zi1] +
                xt * yt * zt * grid[xi1, yi1, zi1];

            return new InterpolationResult { Value = value, XClamped = xClamped, YClamped = yClamped, ZClamped = zClamped };
        }

        /// <summary>
        /// Finds the pair of indices in an ascending array that bracket v, and the fractional
        /// position (0..1) of v between them. If v is outside the array's range, both indices
        /// are set to the nearest edge (fraction 0) and clamped=true is returned.
        /// </summary>
        private static (int lowIndex, int highIndex, double fraction, bool clamped) FindBracket(double[] values, double v)
        {
            int n = values.Length;
            if (n == 1)
                return (0, 0, 0.0, v != values[0]);

            if (v <= values[0])
                return (0, 0, 0.0, v < values[0]);

            if (v >= values[n - 1])
                return (n - 1, n - 1, 0.0, v > values[n - 1]);

            for (int i = 0; i < n - 1; i++)
            {
                if (v >= values[i] && v <= values[i + 1])
                {
                    double span = values[i + 1] - values[i];
                    double t = span > 0 ? (v - values[i]) / span : 0.0;
                    return (i, i + 1, t, false);
                }
            }

            // Should not be reached for a well-formed ascending array.
            return (n - 1, n - 1, 0.0, true);
        }
    }
}
