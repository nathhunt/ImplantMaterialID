using System;
using System.Linq;
using ImplantMaterialID.Models;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class ImplantMaterialClassifierTests
    {
        // A grid point (10 mm, 1.073 mm, 1500 HU), so expected values are exact table entries.
        private const int D = 3, Px = 2, Thr = 1;
        private static double Diameter => PercentileReferenceData.DiametersMm[D];
        private static double PixelSize => PercentileReferenceData.PixelSizesMm[Px];
        private static double Threshold => PercentileReferenceData.ThresholdsHu[Thr];

        private static double ExpectedSs =>
            (PercentileReferenceData.StainlessSteelP70Hu[D, Px, Thr] + PercentileReferenceData.StainlessSteelP80Hu[D, Px, Thr] + PercentileReferenceData.StainlessSteelP90Hu[D, Px, Thr]) / 3.0;

        private static double ExpectedTi =>
            (PercentileReferenceData.TitaniumP70Hu[D, Px, Thr] + PercentileReferenceData.TitaniumP80Hu[D, Px, Thr] + PercentileReferenceData.TitaniumP90Hu[D, Px, Thr]) / 3.0;

        private static MaterialClassificationResult ClassifyAtGridPoint(double p70, double p80, double p90, long voxels = 5000) =>
            ImplantMaterialClassifier.Classify(p70, p80, p90, voxels, Diameter, PixelSize, Threshold);

        // --- The phantom itself ---------------------------------------------------------------

        [Theory]
        [MemberData(nameof(PhantomData.RodCases), MemberType = typeof(PhantomData))]
        public void EveryPhantomRod_IsClassifiedCorrectlyByBothChecks(string structureSet, string structure)
        {
            var rod = PhantomData.Find(structureSet, structure);

            var result = ImplantMaterialClassifier.Classify(
                rod.P70Hu, rod.P80Hu, rod.P90Hu, rod.VoxelCount, rod.DiameterMm, rod.PixelSizeMm, rod.ThresholdHu);

            Assert.Equal(rod.Material, result.GlobalVerdict);
            Assert.Equal(rod.Material, result.MatchedVerdict);
            Assert.True(result.ClassifiersAgree);
            Assert.Equal(rod.Material, result.FinalMaterial);
            Assert.NotEqual(ResultConfidence.Inconclusive, result.Confidence);
        }

        [Fact]
        public void PhantomRodsWithEnoughVoxels_AreHighConfidence()
        {
            foreach (var rod in PhantomData.Rods.Where(r => r.VoxelCount >= ImplantMaterialClassifier.LowVoxelCountWarning))
            {
                var result = ImplantMaterialClassifier.Classify(
                    rod.P70Hu, rod.P80Hu, rod.P90Hu, rod.VoxelCount, rod.DiameterMm, rod.PixelSizeMm, rod.ThresholdHu);

                Assert.True(result.Confidence == ResultConfidence.High, $"{rod}: {string.Join(" | ", result.Warnings)}");
            }
        }

        // --- Inputs and metrics ------------------------------------------------------------------

        [Fact]
        public void InputsAreEchoedOnTheResult()
        {
            var r = ImplantMaterialClassifier.Classify(1, 2, 3, 456, 9.5, 0.9, 1750);

            Assert.Equal(1, r.P70Hu);
            Assert.Equal(2, r.P80Hu);
            Assert.Equal(3, r.P90Hu);
            Assert.Equal(456, r.VoxelCount);
            Assert.Equal(9.5, r.DiameterMm);
            Assert.Equal(0.9, r.PixelSizeMm);
            Assert.Equal(1750, r.ThresholdHu);
        }

        [Fact]
        public void Metrics_AreTheDocumentedPercentileAverages()
        {
            var r = ClassifyAtGridPoint(9000, 12000, 15000);

            Assert.Equal((12000 + 15000) / 2.0, r.GlobalMetricHu);
            Assert.Equal((9000 + 12000 + 15000) / 3.0, r.MatchedMetricHu);
        }

        // --- Classifier 1: global cutoff ----------------------------------------------------------

        [Fact]
        public void Global_AboveCutoffIsStainlessSteel_AtOrBelowIsTitanium()
        {
            double cutoff = ImplantMaterialClassifier.DefaultGlobalCutoffHu;

            Assert.Equal(LikelyMaterial.StainlessSteel, ClassifyAtGridPoint(0, cutoff, cutoff + 2).GlobalVerdict);
            Assert.Equal(LikelyMaterial.Titanium, ClassifyAtGridPoint(0, cutoff, cutoff).GlobalVerdict);
            Assert.Equal(LikelyMaterial.Titanium, ClassifyAtGridPoint(0, cutoff - 2, cutoff).GlobalVerdict);
        }

        [Fact]
        public void Global_MarginIsPercentageDistanceFromCutoff()
        {
            Assert.Equal(10.0, ClassifyAtGridPoint(0, 12100, 12100).GlobalMarginPercent, 9);
            Assert.Equal(10.0, ClassifyAtGridPoint(0, 9900, 9900).GlobalMarginPercent, 9);
            Assert.Equal(ImplantMaterialClassifier.DefaultGlobalCutoffHu, ClassifyAtGridPoint(0, 1, 1).GlobalCutoffHu);
        }

        [Fact]
        public void Global_CustomCutoffIsUsed()
        {
            var r = ImplantMaterialClassifier.Classify(0, 9000, 9000, 5000, Diameter, PixelSize, Threshold, globalCutoffHu: 8000);

            Assert.Equal(8000, r.GlobalCutoffHu);
            Assert.Equal(LikelyMaterial.StainlessSteel, r.GlobalVerdict);
            Assert.Equal(12.5, r.GlobalMarginPercent, 9);
        }

        // --- Classifier 2: matched lookup ---------------------------------------------------------

        [Fact]
        public void Matched_AtGridPoint_ExpectedValuesAreTableAverages()
        {
            var r = ClassifyAtGridPoint(10000, 11000, 12000);

            Assert.Equal(ExpectedSs, r.ExpectedStainlessSteelHu, 9);
            Assert.Equal(ExpectedTi, r.ExpectedTitaniumHu, 9);
            Assert.Equal(Math.Sqrt(ExpectedSs * ExpectedTi), r.MatchedCutoffHu, 9);
        }

        [Fact]
        public void Matched_BetweenGridPoints_InterpolatesTheTables()
        {
            // Halfway between two diameters, everything else on the grid: expected value is the
            // mean of the two neighbouring grid points.
            double d = (PercentileReferenceData.DiametersMm[D] + PercentileReferenceData.DiametersMm[D + 1]) / 2.0;
            double nextSs = (PercentileReferenceData.StainlessSteelP70Hu[D + 1, Px, Thr] + PercentileReferenceData.StainlessSteelP80Hu[D + 1, Px, Thr] + PercentileReferenceData.StainlessSteelP90Hu[D + 1, Px, Thr]) / 3.0;
            double nextTi = (PercentileReferenceData.TitaniumP70Hu[D + 1, Px, Thr] + PercentileReferenceData.TitaniumP80Hu[D + 1, Px, Thr] + PercentileReferenceData.TitaniumP90Hu[D + 1, Px, Thr]) / 3.0;

            var r = ImplantMaterialClassifier.Classify(10000, 11000, 12000, 5000, d, PixelSize, Threshold);

            Assert.Equal((ExpectedSs + nextSs) / 2.0, r.ExpectedStainlessSteelHu, 6);
            Assert.Equal((ExpectedTi + nextTi) / 2.0, r.ExpectedTitaniumHu, 6);
            Assert.DoesNotContain(r.Warnings, w => w.Contains("outside the range"));
        }

        [Fact]
        public void Matched_MeasuringExactlyTheStainlessSteelTable_GivesMarginOne()
        {
            var r = ClassifyAtGridPoint(
                PercentileReferenceData.StainlessSteelP70Hu[D, Px, Thr],
                PercentileReferenceData.StainlessSteelP80Hu[D, Px, Thr],
                PercentileReferenceData.StainlessSteelP90Hu[D, Px, Thr]);

            Assert.Equal(LikelyMaterial.StainlessSteel, r.MatchedVerdict);
            Assert.Equal(1.0, r.MatchedMargin, 9);
        }

        [Fact]
        public void Matched_MeasuringExactlyTheTitaniumTable_GivesMarginOne()
        {
            var r = ClassifyAtGridPoint(
                PercentileReferenceData.TitaniumP70Hu[D, Px, Thr],
                PercentileReferenceData.TitaniumP80Hu[D, Px, Thr],
                PercentileReferenceData.TitaniumP90Hu[D, Px, Thr]);

            Assert.Equal(LikelyMaterial.Titanium, r.MatchedVerdict);
            Assert.Equal(1.0, r.MatchedMargin, 9);
        }

        [Fact]
        public void Matched_OnTheBoundary_IsTitaniumWithZeroMargin()
        {
            double cutoff = Math.Sqrt(ExpectedSs * ExpectedTi);

            var r = ClassifyAtGridPoint(cutoff, cutoff, cutoff);

            Assert.Equal(LikelyMaterial.Titanium, r.MatchedVerdict);
            Assert.Equal(0.0, r.MatchedMargin, 9);
        }

        [Fact]
        public void Matched_NonPositiveMetric_GivesZeroMarginInsteadOfNaN()
        {
            var r = ClassifyAtGridPoint(-100, -100, -100);

            Assert.Equal(LikelyMaterial.Titanium, r.MatchedVerdict);
            Assert.Equal(0.0, r.MatchedMargin);
        }

        // --- Combined decision ---------------------------------------------------------------------

        [Fact]
        public void BothChecksAgree_ReportsThatMaterial()
        {
            var r = ClassifyAtGridPoint(
                PercentileReferenceData.TitaniumP70Hu[D, Px, Thr],
                PercentileReferenceData.TitaniumP80Hu[D, Px, Thr],
                PercentileReferenceData.TitaniumP90Hu[D, Px, Thr]);

            Assert.True(r.ClassifiersAgree);
            Assert.Equal(LikelyMaterial.Titanium, r.FinalMaterial);
            Assert.Empty(r.Warnings);
            Assert.Equal(ResultConfidence.High, r.Confidence);
        }

        [Fact]
        public void ChecksDisagree_IsIndeterminateWithAWarning()
        {
            // Upper tail just over the global cutoff (-> SS), but a very low P70 drags the
            // matched metric well under the matched boundary (-> Ti).
            var r = ClassifyAtGridPoint(0, 11500, 11500);

            Assert.Equal(LikelyMaterial.StainlessSteel, r.GlobalVerdict);
            Assert.Equal(LikelyMaterial.Titanium, r.MatchedVerdict);
            Assert.False(r.ClassifiersAgree);
            Assert.Equal(LikelyMaterial.Indeterminate, r.FinalMaterial);
            Assert.Equal(ResultConfidence.Inconclusive, r.Confidence);
            Assert.Single(r.Warnings);
            Assert.Contains("Check 1 says Stainless steel but Check 2 says Titanium", r.Warnings[0]);
        }

        // --- Warnings --------------------------------------------------------------------------------

        [Fact]
        public void FewerThan100Voxels_IsFlagged()
        {
            var ss = new[]
            {
                PercentileReferenceData.StainlessSteelP70Hu[D, Px, Thr],
                PercentileReferenceData.StainlessSteelP80Hu[D, Px, Thr],
                PercentileReferenceData.StainlessSteelP90Hu[D, Px, Thr],
            };

            var small = ClassifyAtGridPoint(ss[0], ss[1], ss[2], voxels: ImplantMaterialClassifier.LowVoxelCountWarning - 1);
            var enough = ClassifyAtGridPoint(ss[0], ss[1], ss[2], voxels: ImplantMaterialClassifier.LowVoxelCountWarning);

            Assert.Single(small.Warnings);
            Assert.Contains("very small (99 voxels)", small.Warnings[0]);
            Assert.Equal(LikelyMaterial.StainlessSteel, small.FinalMaterial);
            Assert.Equal(ResultConfidence.ReviewRecommended, small.Confidence);
            Assert.Empty(enough.Warnings);
        }

        [Theory]
        [InlineData(2.0, 1.073, 1500, "implant diameter")]
        [InlineData(20.0, 1.073, 1500, "implant diameter")]
        [InlineData(10.0, 0.5, 1500, "CT pixel size")]
        [InlineData(10.0, 1.5, 1500, "CT pixel size")]
        [InlineData(10.0, 1.073, 800, "contouring threshold")]
        [InlineData(10.0, 1.073, 2500, "contouring threshold")]
        public void OutOfCalibratedRange_IsFlagged(double diameter, double pixelSize, double threshold, string flaggedInput)
        {
            var r = ImplantMaterialClassifier.Classify(17000, 18000, 19000, 5000, diameter, pixelSize, threshold);

            var clampWarnings = r.Warnings.Where(w => w.Contains("outside the range")).ToList();
            Assert.Single(clampWarnings);
            Assert.Contains(flaggedInput, clampWarnings[0]);
        }

        [Fact]
        public void EverythingOutOfRange_FlagsEachInputSeparately()
        {
            var r = ImplantMaterialClassifier.Classify(17000, 18000, 19000, 5000, 30, 2, 3000);

            Assert.Equal(3, r.Warnings.Count(w => w.Contains("outside the range")));
        }

        [Theory]
        [InlineData(4.0, 0.624, 1000)]
        [InlineData(16.0, 1.367, 2000)]
        [InlineData(9.0, 0.9, 1234)]
        public void WithinCalibratedRange_IsNotFlagged(double diameter, double pixelSize, double threshold)
        {
            var r = ImplantMaterialClassifier.Classify(17000, 18000, 19000, 5000, diameter, pixelSize, threshold);

            Assert.DoesNotContain(r.Warnings, w => w.Contains("outside the range"));
        }
    }
}
