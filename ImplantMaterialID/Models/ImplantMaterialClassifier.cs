using System;

namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Compares a measured mean HU (from a contoured structure) against interpolated
    /// Stainless Steel / Titanium reference curves for the given implant diameter and
    /// scan FOV, and decides which material is the more likely match.
    /// </summary>
    public static class ImplantMaterialClassifier
    {
        /// <summary>Default agreement tolerance, per spec: expect agreement to the table within 1000 HU.</summary>
        public const double DefaultAgreementToleranceHu = 1000.0;

        public static MaterialClassificationResult Classify(
            double measuredMeanHu,
            double diameterMm,
            double fovMm,
            double agreementToleranceHu = DefaultAgreementToleranceHu)
        {
            var ss = BilinearInterpolator.Interpolate(
                MaterialReferenceData.Diameters, MaterialReferenceData.FovsMm, MaterialReferenceData.StainlessSteelHu,
                diameterMm, fovMm);

            var ti = BilinearInterpolator.Interpolate(
                MaterialReferenceData.Diameters, MaterialReferenceData.FovsMm, MaterialReferenceData.TitaniumHu,
                diameterMm, fovMm);

            double diffSs = Math.Abs(measuredMeanHu - ss.Value);
            double diffTi = Math.Abs(measuredMeanHu - ti.Value);

            var result = new MaterialClassificationResult
            {
                MeasuredMeanHu = measuredMeanHu,
                DiameterMm = diameterMm,
                FovMm = fovMm,
                ExpectedStainlessSteelHu = ss.Value,
                ExpectedTitaniumHu = ti.Value,
                DifferenceFromStainlessSteel = diffSs,
                DifferenceFromTitanium = diffTi,
                AgreementToleranceHu = agreementToleranceHu,
            };

            if (diffSs <= diffTi)
            {
                result.BestMatch = LikelyMaterial.StainlessSteel;
                result.BestMatchDifference = diffSs;
            }
            else
            {
                result.BestMatch = LikelyMaterial.Titanium;
                result.BestMatchDifference = diffTi;
            }

            result.WithinTolerance = result.BestMatchDifference <= agreementToleranceHu;
            if (!result.WithinTolerance)
            {
                // Don't force a call the data doesn't support - flag it instead of silently
                // reporting "closer of two bad fits" as if it were a confident identification.
                result.BestMatch = LikelyMaterial.Indeterminate;
                result.Warnings.Add(
                    $"Measured mean HU ({measuredMeanHu:0}) is more than {agreementToleranceHu:0} HU from " +
                    $"both reference curves (Δ SS={diffSs:0}, Δ Ti={diffTi:0}). Treat this as inconclusive - " +
                    "check contour quality, metal artefact, diameter entry, and whether the implant is a " +
                    "material not covered by these two tables.");
            }

            double dMin = MaterialReferenceData.Diameters[0];
            double dMax = MaterialReferenceData.Diameters[MaterialReferenceData.Diameters.Length - 1];
            if (ss.XClamped || ti.XClamped)
            {
                result.Warnings.Add(
                    $"Entered diameter ({diameterMm:0.#} mm) is outside the reference table range " +
                    $"({dMin:0}-{dMax:0} mm) and was clamped to the nearest edge for interpolation. " +
                    "Treat the result with extra caution.");
            }

            double fMin = MaterialReferenceData.FovsMm[0];
            double fMax = MaterialReferenceData.FovsMm[MaterialReferenceData.FovsMm.Length - 1];
            if (ss.YClamped || ti.YClamped)
            {
                result.Warnings.Add(
                    $"Scan FOV ({fovMm:0} mm) is outside the reference table range ({fMin:0}-{fMax:0} mm) " +
                    "and was clamped to the nearest edge for interpolation. Treat the result with extra caution.");
            }

            return result;
        }
    }
}
