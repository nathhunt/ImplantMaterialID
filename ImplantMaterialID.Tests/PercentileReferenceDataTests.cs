using System;
using System.Collections.Generic;
using System.Linq;
using ImplantMaterialID.Models;
using Xunit;

namespace ImplantMaterialID.Tests
{
    /// <summary>
    /// PercentileReferenceData.cs is generated from the phantom CSV. These check the generated
    /// file is well-formed and hasn't drifted from the CSV (e.g. the CSV was updated but the
    /// generator wasn't re-run, or the generated file was edited by hand).
    /// </summary>
    public class PercentileReferenceDataTests
    {
        public static IEnumerable<object[]> AllGrids()
        {
            yield return new object[] { "StainlessSteelP70Hu" };
            yield return new object[] { "StainlessSteelP80Hu" };
            yield return new object[] { "StainlessSteelP90Hu" };
            yield return new object[] { "TitaniumP70Hu" };
            yield return new object[] { "TitaniumP80Hu" };
            yield return new object[] { "TitaniumP90Hu" };
        }

        private static double[,,] Grid(string name)
        {
            switch (name)
            {
                case "StainlessSteelP70Hu": return PercentileReferenceData.StainlessSteelP70Hu;
                case "StainlessSteelP80Hu": return PercentileReferenceData.StainlessSteelP80Hu;
                case "StainlessSteelP90Hu": return PercentileReferenceData.StainlessSteelP90Hu;
                case "TitaniumP70Hu": return PercentileReferenceData.TitaniumP70Hu;
                case "TitaniumP80Hu": return PercentileReferenceData.TitaniumP80Hu;
                case "TitaniumP90Hu": return PercentileReferenceData.TitaniumP90Hu;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        [Fact]
        public void Axes_AreStrictlyAscending()
        {
            AssertStrictlyAscending(PercentileReferenceData.DiametersMm);
            AssertStrictlyAscending(PercentileReferenceData.PixelSizesMm);
            AssertStrictlyAscending(PercentileReferenceData.ThresholdsHu);
        }

        [Theory]
        [MemberData(nameof(AllGrids))]
        public void Grid_ShapeMatchesAxes(string gridName)
        {
            var grid = Grid(gridName);

            Assert.Equal(PercentileReferenceData.DiametersMm.Length, grid.GetLength(0));
            Assert.Equal(PercentileReferenceData.PixelSizesMm.Length, grid.GetLength(1));
            Assert.Equal(PercentileReferenceData.ThresholdsHu.Length, grid.GetLength(2));
        }

        [Fact]
        public void AxesMatchPhantomCsv()
        {
            Assert.Equal(PhantomData.Rods.Select(r => r.DiameterMm).Distinct().OrderBy(v => v), PercentileReferenceData.DiametersMm);
            Assert.Equal(PhantomData.Rods.Select(r => r.PixelSizeMm).Distinct().OrderBy(v => v), PercentileReferenceData.PixelSizesMm);
            Assert.Equal(PhantomData.Rods.Select(r => r.ThresholdHu).Distinct().OrderBy(v => v), PercentileReferenceData.ThresholdsHu);
        }

        [Fact]
        public void PhantomCsv_HasExactlyOneRowPerGridPointPerMaterial()
        {
            int gridPoints = PercentileReferenceData.DiametersMm.Length
                             * PercentileReferenceData.PixelSizesMm.Length
                             * PercentileReferenceData.ThresholdsHu.Length;

            foreach (var material in new[] { LikelyMaterial.StainlessSteel, LikelyMaterial.Titanium })
            {
                var rows = PhantomData.Rods.Where(r => r.Material == material).ToList();
                Assert.Equal(gridPoints, rows.Count);
                Assert.Equal(gridPoints, rows.Select(r => (r.DiameterMm, r.PixelSizeMm, r.ThresholdHu)).Distinct().Count());
            }
        }

        [Theory]
        [MemberData(nameof(PhantomData.RodCases), MemberType = typeof(PhantomData))]
        public void GridValues_MatchPhantomCsv(string structureSet, string structure)
        {
            var rod = PhantomData.Find(structureSet, structure);
            int i = Array.IndexOf(PercentileReferenceData.DiametersMm, rod.DiameterMm);
            int j = Array.IndexOf(PercentileReferenceData.PixelSizesMm, rod.PixelSizeMm);
            int k = Array.IndexOf(PercentileReferenceData.ThresholdsHu, rod.ThresholdHu);
            bool ss = rod.Material == LikelyMaterial.StainlessSteel;

            Assert.Equal(rod.P70Hu, (ss ? PercentileReferenceData.StainlessSteelP70Hu : PercentileReferenceData.TitaniumP70Hu)[i, j, k]);
            Assert.Equal(rod.P80Hu, (ss ? PercentileReferenceData.StainlessSteelP80Hu : PercentileReferenceData.TitaniumP80Hu)[i, j, k]);
            Assert.Equal(rod.P90Hu, (ss ? PercentileReferenceData.StainlessSteelP90Hu : PercentileReferenceData.TitaniumP90Hu)[i, j, k]);
        }

        [Fact]
        public void PercentilesAreOrderedAtEveryGridPoint()
        {
            ForEachGridPoint((i, j, k) =>
            {
                Assert.True(PercentileReferenceData.StainlessSteelP70Hu[i, j, k] <= PercentileReferenceData.StainlessSteelP80Hu[i, j, k]);
                Assert.True(PercentileReferenceData.StainlessSteelP80Hu[i, j, k] <= PercentileReferenceData.StainlessSteelP90Hu[i, j, k]);
                Assert.True(PercentileReferenceData.TitaniumP70Hu[i, j, k] <= PercentileReferenceData.TitaniumP80Hu[i, j, k]);
                Assert.True(PercentileReferenceData.TitaniumP80Hu[i, j, k] <= PercentileReferenceData.TitaniumP90Hu[i, j, k]);
            });
        }

        [Fact]
        public void StainlessSteelExceedsTitaniumAtEveryGridPoint()
        {
            // The classifier throws if this ever fails for an interpolated point; checking every
            // grid point guarantees it can't, since interpolation is a convex combination.
            ForEachGridPoint((i, j, k) =>
            {
                double ss = PercentileReferenceData.StainlessSteelP70Hu[i, j, k] + PercentileReferenceData.StainlessSteelP80Hu[i, j, k] + PercentileReferenceData.StainlessSteelP90Hu[i, j, k];
                double ti = PercentileReferenceData.TitaniumP70Hu[i, j, k] + PercentileReferenceData.TitaniumP80Hu[i, j, k] + PercentileReferenceData.TitaniumP90Hu[i, j, k];
                Assert.True(ti > 0);
                Assert.True(ss > ti, $"SS <= Ti at grid point [{i}, {j}, {k}]");
            });
        }

        private static void ForEachGridPoint(Action<int, int, int> check)
        {
            for (int i = 0; i < PercentileReferenceData.DiametersMm.Length; i++)
                for (int j = 0; j < PercentileReferenceData.PixelSizesMm.Length; j++)
                    for (int k = 0; k < PercentileReferenceData.ThresholdsHu.Length; k++)
                        check(i, j, k);
        }

        private static void AssertStrictlyAscending(double[] values)
        {
            Assert.NotEmpty(values);
            for (int i = 1; i < values.Length; i++)
                Assert.True(values[i] > values[i - 1], $"Axis not strictly ascending at index {i}.");
        }
    }
}
