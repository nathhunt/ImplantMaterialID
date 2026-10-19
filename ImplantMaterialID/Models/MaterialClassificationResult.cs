using System.Collections.Generic;

namespace ImplantMaterialID.Models
{
    public enum LikelyMaterial
    {
        StainlessSteel,
        Titanium,
        Indeterminate
    }

    public enum ResultConfidence
    {
        /// <summary>Both checks agree and nothing was flagged.</summary>
        High,

        /// <summary>Both checks agree, but something was flagged (out of range, small structure).</summary>
        ReviewRecommended,

        /// <summary>The two checks disagree - no material is reported.</summary>
        Inconclusive
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

        /// <summary>Reasons the result was flagged for review, shown in the detailed breakdown.</summary>
        public List<string> Warnings { get; } = new List<string>();

        public bool HasWarnings => Warnings.Count > 0;

        // --- Plain-language presentation ------------------------------------------

        public ResultConfidence Confidence =>
            !ClassifiersAgree ? ResultConfidence.Inconclusive
            : Warnings.Count > 0 ? ResultConfidence.ReviewRecommended
            : ResultConfidence.High;

        /// <summary>The one-line answer, e.g. "Titanium" or "Unable to determine".</summary>
        public string Headline =>
            FinalMaterial == LikelyMaterial.Indeterminate ? "Unable to determine" : Display(FinalMaterial);

        public string ConfidenceLabel
        {
            get
            {
                switch (Confidence)
                {
                    case ResultConfidence.High: return "Both checks agree";
                    case ResultConfidence.ReviewRecommended: return "Both checks agree - review recommended";
                    default: return "The two checks disagree";
                }
            }
        }

        public string Explanation
        {
            get
            {
                switch (Confidence)
                {
                    case ResultConfidence.High:
                        return $"The implant's brightness on the CT matches {Display(FinalMaterial).ToLowerInvariant()}, " +
                               "and both independent checks give the same answer.";
                    case ResultConfidence.ReviewRecommended:
                        return $"Both checks point to {Display(FinalMaterial).ToLowerInvariant()}, but the result is less " +
                               "certain than usual. Consult a physicist before relying on it.";
                    default:
                        return "The two checks gave different answers, so no material can be reported. Consult a physicist.";
                }
            }
        }

        public string GlobalVerdictText => Display(GlobalVerdict);
        public string MatchedVerdictText => Display(MatchedVerdict);

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
