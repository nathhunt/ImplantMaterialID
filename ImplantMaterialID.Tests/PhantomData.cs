using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImplantMaterialID.Models;

namespace ImplantMaterialID.Tests
{
    /// <summary>
    /// One row of calibration/Combined_HU_percentiles.csv - a contoured phantom rod of known material.
    /// </summary>
    public sealed class PhantomRod
    {
        public string StructureSet { get; set; }
        public string Structure { get; set; }
        public LikelyMaterial Material { get; set; }
        public double DiameterMm { get; set; }
        public double ThresholdHu { get; set; }
        public double PixelSizeMm { get; set; }
        public long VoxelCount { get; set; }
        public double P70Hu { get; set; }
        public double P80Hu { get; set; }
        public double P90Hu { get; set; }

        public override string ToString() => $"{StructureSet}/{Structure}";
    }

    /// <summary>
    /// Reads the phantom CSV the same way calibration/generate_reference_data.py does. The CSV is
    /// copied next to the test assembly by the test project.
    /// </summary>
    public static class PhantomData
    {
        private static readonly Lazy<IReadOnlyList<PhantomRod>> LazyRods = new Lazy<IReadOnlyList<PhantomRod>>(Load);

        public static IReadOnlyList<PhantomRod> Rods => LazyRods.Value;

        /// <summary>Rows as xUnit MemberData: one case per rod, identified by structure set and structure name.</summary>
        public static IEnumerable<object[]> RodCases() =>
            Rods.Select(r => new object[] { r.StructureSet, r.Structure });

        public static PhantomRod Find(string structureSet, string structure) =>
            Rods.Single(r => r.StructureSet == structureSet && r.Structure == structure);

        private static IReadOnlyList<PhantomRod> Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "calibration", "Combined_HU_percentiles.csv");
            var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToArray();
            var header = lines[0].Split(',');
            int Col(string name) => Array.IndexOf(header, name);

            var rods = new List<PhantomRod>();
            foreach (var line in lines.Skip(1))
            {
                var f = line.Split(',');
                double Num(string name) => double.Parse(f[Col(name)], CultureInfo.InvariantCulture);

                var structureSet = f[Col("StructureSet")];
                var structure = f[Col("Structure")];
                var nameParts = structure.Split('_');

                rods.Add(new PhantomRod
                {
                    StructureSet = structureSet,
                    Structure = structure,
                    Material = structureSet.StartsWith("SS", StringComparison.Ordinal) ? LikelyMaterial.StainlessSteel
                        : structureSet.StartsWith("Ti", StringComparison.Ordinal) ? LikelyMaterial.Titanium
                        : throw new InvalidDataException($"Unrecognised material prefix in '{structureSet}'."),
                    DiameterMm = double.Parse(nameParts[0].Replace("mm", ""), CultureInfo.InvariantCulture),
                    ThresholdHu = double.Parse(nameParts[1], CultureInfo.InvariantCulture),
                    PixelSizeMm = (Num("PixelSizeX_mm") + Num("PixelSizeY_mm")) / 2.0,
                    VoxelCount = (long)Num("VoxelCount"),
                    P70Hu = Num("P70_HU"),
                    P80Hu = Num("P80_HU"),
                    P90Hu = Num("P90_HU"),
                });
            }
            return rods;
        }
    }
}
