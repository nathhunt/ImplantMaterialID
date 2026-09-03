using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImplantMaterialID.Models;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// Async, thread-safe-from-the-caller's-perspective abstraction over ESAPI. Every method
    /// returns a Task that completes once the underlying ESAPI call has run on ESAPI's single
    /// dedicated STA thread - callers (the ViewModel) can simply `await` these from the UI
    /// thread without needing to know anything about that threading requirement themselves.
    /// </summary>
    public interface IEsapiPatientService : IDisposable
    {
        /// <summary>Opens the patient and returns the structure sets available for them.</summary>
        Task<IReadOnlyList<StructureSetSummary>> OpenPatientAndGetStructureSetsAsync(string patientId);

        /// <summary>Returns the structures contained in the given structure set.</summary>
        Task<IReadOnlyList<StructureSummary>> GetStructuresAsync(string structureSetId);

        /// <summary>
        /// Computes the mean HU of the named structure and the reconstruction FOV of the
        /// associated CT.
        /// </summary>
        Task<StructureStatistics> ComputeStructureStatisticsAsync(string structureSetId, string structureId);

        /// <summary>Closes the currently open patient, if any (required before opening another).</summary>
        Task ClosePatientAsync();
    }
}
