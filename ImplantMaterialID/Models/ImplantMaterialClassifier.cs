using System;

namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Classifies a contoured rod as stainless steel or titanium from the upper percentiles of
    /// its voxel HU, using two classifiers that must agree:
    ///
    /// 1. Global: avg(P80, P90) against a single cutoff. Needs no metadata - the upper tail is
    ///    dominated by voxels fully inside the metal, so it is barely affected by the
    ///    partial-volume averaging that pulls lower percentiles down in thin rods / coarse pixels.
    /// 2. Matched: avg(P70, P80, P90) against the phantom values for both metals, interpolated at
    ///    this structure's diameter, pixel size and threshold. Uses more voxels than the global
    ///    metric, at the cost of needing the metadata.
    ///
    /// Averaging several percentiles rather than using one (e.g. P90 alone) reduces how much the
    /// result rests on a handful of tail voxels in small structures.
    /// </summary>
    public static class ImplantMaterialClassifier
    {
        /// <summary>
        /// avg(P80, P90) cutoff. In the phantom data, stainless steel never drops below ~12,900 HU
        /// and titanium never exceeds ~9,500 HU, so 11,000 HU leaves ~17% / ~14% margin either side.
        /// </summary>
        public const double DefaultGlobalCutoffHu = 11000.0;

        /// <summary>
        /// Below this many voxels the upper percentiles rest on very few values (P90 of 100 voxels
        /// is set by ~10 of them), so the result is flagged as lower confidence.
        /// </summary>
        public const long LowVoxelCountWarning = 100;

        public static MaterialClassificationResult Classify(
            double p70Hu,
            double p80Hu,
            double p90Hu,
            long voxelCount,
            double diameterMm,
            double pixelSizeMm,
            double thresholdHu,
            double globalCutoffHu = DefaultGlobalCutoffHu)
        {
            var result = new MaterialClassificationResult
            {
                P70Hu = p70Hu,
                P80Hu = p80Hu,
                P90Hu = p90Hu,
                VoxelCount = voxelCount,
                DiameterMm = diameterMm,
                PixelSizeMm = pixelSizeMm,
                ThresholdHu = thresholdHu,
            };

            // --- Classifier 1: global cutoff ---
            result.GlobalMetricHu = (p80Hu + p90Hu) / 2.0;
            result.GlobalCutoffHu = globalCutoffHu;
            result.GlobalVerdict = result.GlobalMetricHu > globalCutoffHu ? LikelyMaterial.StainlessSteel : LikelyMaterial.Titanium;
            result.GlobalMarginPercent = Math.Abs(result.GlobalMetricHu / globalCutoffHu - 1.0) * 100.0;

            // --- Classifier 2: matched lookup ---
            // Interpolation is linear, so interpolating each percentile then averaging is the same
            // as interpolating the average.
            var ss70 = Lookup(PercentileReferenceData.StainlessSteelP70Hu, diameterMm, pixelSizeMm, thresholdHu);
            var ss80 = Lookup(PercentileReferenceData.StainlessSteelP80Hu, diameterMm, pixelSizeMm, thresholdHu);
            var ss90 = Lookup(PercentileReferenceData.StainlessSteelP90Hu, diameterMm, pixelSizeMm, thresholdHu);
            var ti70 = Lookup(PercentileReferenceData.TitaniumP70Hu, diameterMm, pixelSizeMm, thresholdHu);
            var ti80 = Lookup(PercentileReferenceData.TitaniumP80Hu, diameterMm, pixelSizeMm, thresholdHu);
            var ti90 = Lookup(PercentileReferenceData.TitaniumP90Hu, diameterMm, pixelSizeMm, thresholdHu);

            double expectedSs = (ss70.Value + ss80.Value + ss90.Value) / 3.0;
            double expectedTi = (ti70.Value + ti80.Value + ti90.Value) / 3.0;
            if (expectedSs <= expectedTi || expectedTi <= 0)
                throw new InvalidOperationException(
                    "Reference tables are inconsistent: expected stainless steel HU must exceed expected titanium HU.");

            result.MatchedMetricHu = (p70Hu + p80Hu + p90Hu) / 3.0;
            result.ExpectedStainlessSteelHu = expectedSs;
            result.ExpectedTitaniumHu = expectedTi;

            // Compare on a ratio (log) scale: SS sits at roughly twice Ti, so an absolute-HU
            // midpoint would give titanium a much narrower band than stainless steel.
            result.MatchedCutoffHu = Math.Sqrt(expectedSs * expectedTi);
            result.MatchedVerdict = result.MatchedMetricHu > result.MatchedCutoffHu ? LikelyMaterial.StainlessSteel : LikelyMaterial.Titanium;
            result.MatchedMargin = result.MatchedMetricHu > 0
                ? Math.Abs(Math.Log(result.MatchedMetricHu / result.MatchedCutoffHu)) / (0.5 * Math.Log(expectedSs / expectedTi))
                : 0.0;

            // --- Combined decision ---
            result.ClassifiersAgree = result.GlobalVerdict == result.MatchedVerdict;
            result.FinalMaterial = result.ClassifiersAgree ? result.GlobalVerdict : LikelyMaterial.Indeterminate;

            if (!result.ClassifiersAgree)
            {
                result.Warnings.Add(
                    $"Check 1 says {MaterialClassificationResult.Display(result.GlobalVerdict)} but Check 2 says " +
                    $"{MaterialClassificationResult.Display(result.MatchedVerdict)}. Possible causes: wrong diameter or " +
                    "threshold entered, contour including non-implant voxels, heavy streak artefact, or a material not in " +
                    "the reference data.");
            }

            if (voxelCount < LowVoxelCountWarning)
            {
                result.Warnings.Add(
                    $"The contour is very small ({voxelCount} voxels), so the measurement is based on only a few points " +
                    "and is more easily affected by image noise.");
            }

            AddClampWarning(result, ss70.XClamped, "implant diameter entered", $"{diameterMm:0.#} mm", PercentileReferenceData.DiametersMm, "0", "mm");
            AddClampWarning(result, ss70.YClamped, "CT pixel size", $"{pixelSizeMm:0.###} mm", PercentileReferenceData.PixelSizesMm, "0.###", "mm");
            AddClampWarning(result, ss70.ZClamped, "contouring threshold entered", $"{thresholdHu:0} HU", PercentileReferenceData.ThresholdsHu, "0", "HU");

            return result;
        }

        private static InterpolationResult Lookup(double[,,] grid, double diameterMm, double pixelSizeMm, double thresholdHu) =>
            TrilinearInterpolator.Interpolate(
                PercentileReferenceData.DiametersMm, PercentileReferenceData.PixelSizesMm, PercentileReferenceData.ThresholdsHu,
                grid, diameterMm, pixelSizeMm, thresholdHu);

        private static void AddClampWarning(MaterialClassificationResult result, bool clamped, string name, string value, double[] axis, string format, string unit)
        {
            if (!clamped)
                return;

            result.Warnings.Add(
                $"The {name} ({value}) is outside the range this tool was calibrated for " +
                $"({axis[0].ToString(format)}-{axis[axis.Length - 1].ToString(format)} {unit}), so the nearest calibrated value was used.");
        }
    }
}
