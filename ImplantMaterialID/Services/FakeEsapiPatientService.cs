using System.Collections.Generic;
using System.Threading.Tasks;
using ImplantMaterialID.Models;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// In-memory stand-in for <see cref="StaEsapiPatientService"/> that returns canned data
    /// instead of talking to Eclipse. Useful for developing/demoing the UI, and for manually
    /// exercising the MVVM wiring, before you have ESAPI DLLs and a test patient available.
    /// Swap this in for StaEsapiPatientService in MainWindow.xaml.cs's composition root.
    ///
    /// This is NOT a substitute for validating the real voxel/HU logic in
    /// EsapiPatientServiceCore - it hard-codes plausible percentiles rather than computing them.
    /// There's no real threading concern here (no live ESAPI objects), so Task.Delay is used
    /// just to simulate realistic latency rather than for any correctness reason.
    /// </summary>
    public class FakeEsapiPatientService : IEsapiPatientService
    {
        public async Task<IReadOnlyList<StructureSetSummary>> OpenPatientAndGetStructureSetsAsync(string patientId)
        {
            await Task.Delay(400);
            return new List<StructureSetSummary>
            {
                new StructureSetSummary { Id = "CT_1", Description = "CT_1  (image: CT_1)" },
                new StructureSetSummary { Id = "CT_2_REPLAN", Description = "CT_2_REPLAN  (image: CT_2)" },
            };
        }

        public async Task<IReadOnlyList<StructureSummary>> GetStructuresAsync(string structureSetId)
        {
            await Task.Delay(300);
            return new List<StructureSummary>
            {
                new StructureSummary { Id = "BODY", DicomType = "EXTERNAL", HasContours = true },
                new StructureSummary { Id = "Implant_Rod", DicomType = "ORGAN", HasContours = true },
                new StructureSummary { Id = "PTV", DicomType = "PTV", HasContours = true },
                new StructureSummary { Id = "Unused_Structure", DicomType = "ORGAN", HasContours = false },
            };
        }

        public async Task<StructureStatistics> ComputeStructureStatisticsAsync(string structureSetId, string structureId)
        {
            await Task.Delay(800); // the real voxel loop is the slow step - simulate that here too

            // Canned numbers from the 10 mm titanium rod, 1.073 mm pixels, 1500 HU threshold row
            // of the phantom CSV - enter diameter 10 and threshold 1500 to exercise the
            // "Titanium, both classifiers agree" path end-to-end.
            return new StructureStatistics
            {
                P70Hu = 8235,
                P80Hu = 8402,
                P90Hu = 8650,
                VoxelCount = 296,
                PixelSizeXMm = 1.073,
                PixelSizeYMm = 1.073,
                PixelSizeMm = 1.073,
                FovMm = 549
            };
        }

        public Task ClosePatientAsync() => Task.CompletedTask;

        public void Dispose() { }
    }
}
