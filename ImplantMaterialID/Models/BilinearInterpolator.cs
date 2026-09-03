using System;

namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Result of interpolating a single 2-D table at one (x, y) query point.
    /// </summary>
    public struct InterpolationResult
    {
        public double Value;

        /// <summary>True if x fell outside the table's range and was clamped to the nearest edge.</summary>
        public bool XClamped;

        /// <summary>True if y fell outside the table's range and was clamped to the nearest edge.</summary>
        public bool YClamped;
    }

    /// <summary>
    /// Bilinear interpolation over an irregularly-spaced-but-monotonic 2-D grid.
    /// Implemented as two nested 1-D linear interpolations, which naturally handles
    /// an exact hit on a grid line (interpolation fraction = 0) without special-casing.
    /// Query points outside the table are clamped to the nearest edge rather than
    /// extrapolated, since linear extrapolation of a CT-number-vs-size curve is not
    /// reliable outside the characterised range.
    /// </summary>
    public static class BilinearInterpolator
    {
        /// <param name="xValues">Ascending grid coordinates for dimension 0 (rows), e.g. diameters.</param>
        /// <param name="yValues">Ascending grid coordinates for dimension 1 (columns), e.g. FOVs.</param>
        /// <param name="grid">grid[i, j] = table value at (xValues[i], yValues[j]).</param>
        /// <param name="x">Query value along the x (row) axis.</param>
        /// <param name="y">Query value along the y (column) axis.</param>
        public static InterpolationResult Interpolate(double[] xValues, double[] yValues, double[,] grid, double x, double y)
        {
            if (xValues == null || yValues == null || grid == null)
                throw new ArgumentNullException("Reference table arrays must not be null.");
            if (grid.GetLength(0) != xValues.Length || grid.GetLength(1) != yValues.Length)
                throw new ArgumentException("Grid dimensions do not match the supplied axis arrays.");

            var (xi0, xi1, xt, xClamped) = FindBracket(xValues, x);
            var (yi0, yi1, yt, yClamped) = FindBracket(yValues, y);

            double f00 = grid[xi0, yi0];
            double f10 = grid[xi1, yi0];
            double f01 = grid[xi0, yi1];
            double f11 = grid[xi1, yi1];

            // Interpolate across x at each of the two bracketing y values, then across y.
            double fAtY0 = Lerp(f00, f10, xt);
            double fAtY1 = Lerp(f01, f11, xt);
            double value = Lerp(fAtY0, fAtY1, yt);

            return new InterpolationResult { Value = value, XClamped = xClamped, YClamped = yClamped };
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

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
