using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImplantMaterialID.Models;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// Composition-root-facing implementation of <see cref="IEsapiPatientService"/>. Owns a
    /// dedicated <see cref="EsapiStaThread"/> and the real <see cref="EsapiPatientServiceCore"/>,
    /// and marshals every call - including construction, which is where
    /// Application.CreateApplication() runs - onto that one thread. This is the class the UI
    /// composition root (MainWindow) should construct; nothing else in the app should reference
    /// EsapiPatientServiceCore directly.
    /// </summary>
    public sealed class StaEsapiPatientService : IEsapiPatientService
    {
        private readonly EsapiStaThread _sta = new EsapiStaThread();
        private EsapiPatientServiceCore _core;

        private async Task<EsapiPatientServiceCore> GetCoreAsync()
        {
            // Application.CreateApplication() (inside the core's constructor) can pop up a
            // native login dialog, so this may not complete until the user signs in - that's
            // fine, it's all still happening on the dedicated ESAPI thread, not the UI thread.
            if (_core == null)
                _core = await _sta.InvokeAsync(() => new EsapiPatientServiceCore());

            return _core;
        }

        public async Task<IReadOnlyList<StructureSetSummary>> OpenPatientAndGetStructureSetsAsync(string patientId)
        {
            var core = await GetCoreAsync();
            return await _sta.InvokeAsync(() => core.OpenPatientAndGetStructureSets(patientId));
        }

        public async Task<IReadOnlyList<StructureSummary>> GetStructuresAsync(string structureSetId)
        {
            var core = await GetCoreAsync();
            return await _sta.InvokeAsync(() => core.GetStructures(structureSetId));
        }

        public async Task<StructureStatistics> ComputeStructureStatisticsAsync(string structureSetId, string structureId)
        {
            var core = await GetCoreAsync();
            return await _sta.InvokeAsync(() => core.ComputeStructureStatistics(structureSetId, structureId));
        }

        public async Task ClosePatientAsync()
        {
            if (_core == null)
                return;

            var core = _core;
            await _sta.InvokeAsync(() => core.ClosePatient());
        }

        public void Dispose()
        {
            // Deliberately synchronous/blocking: this only runs during app shutdown, when
            // there's no UI responsiveness left to protect, and we need ESAPI cleanly closed
            // down before the process exits.
            if (_core != null)
            {
                var core = _core;
                _sta.InvokeAndWait(() => core.Dispose(), TimeSpan.FromSeconds(10));
            }

            _sta.Dispose();
        }
    }
}
