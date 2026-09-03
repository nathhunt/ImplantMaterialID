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
    /// EsapiPatientServiceCore - it hard-codes a plausible mean HU rather than computing one.
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

            // Canned numbers roughly matching a 10mm titanium rod at 550mm FOV, so the demo
            // exercises the "Titanium, within tolerance" path end-to-end.
            return new StructureStatistics
            {
                MeanHu = 9500,
                VoxelCount = 4200,
                FovXMm = 550,
                FovYMm = 550,
                FovMm = 550
            };
        }

        public Task ClosePatientAsync() => Task.CompletedTask;

        public void Dispose() { }
    }
}
