using System;
using System.Collections.Generic;

namespace ImplantMaterialID.Models
{
    public static class Percentile
    {
        /// <summary>
        /// Linear-interpolation percentile of an ascending-sorted sample: position (n - 1) * p,
        /// interpolating between the two neighbouring values. This is Hyndman &amp; Fan's
        /// definition 7, the same as Excel PERCENTILE.INC and numpy's default.
        ///
        /// It must match the definition used to build the reference CSV, otherwise every measured
        /// percentile is shifted relative to the tables - small structures in the steep upper
        /// tail are where definitions differ most. Every value in
        /// calibration/Combined_HU_percentiles.csv is consistent with this definition and
        /// integer voxel HU; none of the other common definitions reproduce all of them.
        /// </summary>
        /// <param name="sortedAscending">Sample values, already sorted ascending.</param>
        /// <param name="fraction">Percentile as a fraction, 0..1 (e.g. 0.9 for P90).</param>
        public static double Linear(IReadOnlyList<double> sortedAscending, double fraction)
        {
            if (sortedAscending == null)
                throw new ArgumentNullException(nameof(sortedAscending));
            if (sortedAscending.Count == 0)
                throw new ArgumentException("Cannot take a percentile of an empty sample.", nameof(sortedAscending));
            if (fraction < 0 || fraction > 1)
                throw new ArgumentOutOfRangeException(nameof(fraction), "Percentile fraction must be between 0 and 1.");

            double h = (sortedAscending.Count - 1) * fraction;
            int lo = (int)Math.Floor(h);
            int hi = Math.Min(lo + 1, sortedAscending.Count - 1);
            return sortedAscending[lo] + (h - lo) * (sortedAscending[hi] - sortedAscending[lo]);
        }
    }
}
