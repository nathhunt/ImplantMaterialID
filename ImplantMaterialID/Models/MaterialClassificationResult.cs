using System.Collections.Generic;

namespace ImplantMaterialID.Models
{
    public enum LikelyMaterial
    {
        StainlessSteel,
        Titanium,
        Indeterminate
    }

    /// <summary>
    /// Full output of the dual percentile classifier: a global cutoff on avg(P80, P90), and a
    /// lookup of avg(P70, P80, P90) matched on diameter, pixel size and threshold.
    /// </summary>
    public class MaterialClassificationResult
    {
        // --- Inputs ----------------------------------------------------------

        public double P70Hu { get; set; }
        public double P80Hu { get; set; }
        public double P90Hu { get; set; }
        public long VoxelCount { get; set; }
        public double DiameterMm { get; set; }
        public double PixelSizeMm { get; set; }
        public double ThresholdHu { get; set; }

        // --- Classifier 1: global cutoff ---------------------------------------

        /// <summary>avg(P80, P90) of the measured structure.</summary>
        public double GlobalMetricHu { get; set; }
        public double GlobalCutoffHu { get; set; }
        public LikelyMaterial GlobalVerdict { get; set; }

        /// <summary>How far the metric sits from the cutoff, as a percentage of the cutoff.</summary>
        public double GlobalMarginPercent { get; set; }

        // --- Classifier 2: matched lookup --------------------------------------

        /// <summary>avg(P70, P80, P90) of the measured structure.</summary>
        public double MatchedMetricHu { get; set; }

        /// <summary>Interpolated avg(P70, P80, P90) expected for each material at this diameter/pixel size/threshold.</summary>
        public double ExpectedStainlessSteelHu { get; set; }
        public double ExpectedTitaniumHu { get; set; }

        /// <summary>Geometric mean of the two expected values - the boundary on a ratio scale.</summary>
        public double MatchedCutoffHu { get; set; }
        public LikelyMaterial MatchedVerdict { get; set; }

        /// <summary>
        /// Normalised log-ratio margin: 1 = measured value lands exactly on the chosen material's
        /// expected value, 0 = on the decision boundary. Always non-negative here, since it is
        /// measured towards whichever material was chosen.
        /// </summary>
        public double MatchedMargin { get; set; }

        // --- Combined decision ---------------------------------------------------

        public bool ClassifiersAgree { get; set; }

        /// <summary>The agreed material, or Indeterminate if the two classifiers disagree.</summary>
        public LikelyMaterial FinalMaterial { get; set; }

        /// <summary>True if the classifiers disagree, or any input is outside the calibrated range or flagged.</summary>
        public bool NeedsReview => !ClassifiersAgree || Warnings.Count > 0;

        public List<string> Warnings { get; } = new List<string>();

        public string GlobalSummary =>
            $"avg(P80, P90) = {GlobalMetricHu:0} HU vs cutoff {GlobalCutoffHu:0} HU → {Display(GlobalVerdict)} " +
            $"(margin {GlobalMarginPercent:0}%)";

        public string MatchedSummary =>
            $"avg(P70, P80, P90) = {MatchedMetricHu:0} HU vs boundary {MatchedCutoffHu:0} HU → {Display(MatchedVerdict)} " +
            $"(margin {MatchedMargin:0.00})";

        public string Summary
        {
            get
            {
                if (!ClassifiersAgree)
                    return "Indeterminate - the two classifiers disagree. Review manually.";

                return NeedsReview
                    ? $"{Display(FinalMaterial)} (both classifiers agree, but see warnings - review before relying on it)"
                    : $"{Display(FinalMaterial)} (both classifiers agree)";
            }
        }

        public static string Display(LikelyMaterial material)
        {
            switch (material)
            {
                case LikelyMaterial.StainlessSteel: return "Stainless steel";
                case LikelyMaterial.Titanium: return "Titanium";
                default: return "Indeterminate";
            }
        }
    }
}
