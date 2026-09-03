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
    /// Full output of comparing a measured mean HU against the interpolated
    /// stainless steel and titanium reference curves for a given diameter/FOV.
    /// </summary>
    public class MaterialClassificationResult
    {
        public double MeasuredMeanHu { get; set; }
        public double DiameterMm { get; set; }
        public double FovMm { get; set; }

        public double ExpectedStainlessSteelHu { get; set; }
        public double ExpectedTitaniumHu { get; set; }

        public double DifferenceFromStainlessSteel { get; set; }
        public double DifferenceFromTitanium { get; set; }

        /// <summary>The material whose interpolated HU is closer to the measured mean HU.</summary>
        public LikelyMaterial BestMatch { get; set; }

        /// <summary>The |measured - expected| difference for BestMatch.</summary>
        public double BestMatchDifference { get; set; }

        /// <summary>
        /// True only if BestMatchDifference is within <see cref="AgreementToleranceHu"/> of the
        /// table. If false, the identification should be treated as unreliable / indeterminate,
        /// per the requirement that agreement with the table should be within 1000 HU.
        /// </summary>
        public bool WithinTolerance { get; set; }

        public double AgreementToleranceHu { get; set; }

        public List<string> Warnings { get; } = new List<string>();

        public string Summary
        {
            get
            {
                string material = BestMatch == LikelyMaterial.Indeterminate
                    ? "Indeterminate"
                    : BestMatch.ToString();

                string qualifier = WithinTolerance
                    ? "within tolerance"
                    : $"OUTSIDE the ±{AgreementToleranceHu:0} HU tolerance - review manually";

                return $"Best match: {material} ({qualifier}, diff={BestMatchDifference:0} HU)";
            }
        }
    }
}
