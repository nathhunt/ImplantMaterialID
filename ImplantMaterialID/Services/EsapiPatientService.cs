using System;
using System.Collections.Generic;
using System.Linq;
using ImplantMaterialID.Models;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// Real ESAPI-backed implementation. All the actual VMS.TPS.Common.Model.API calls live
    /// here, in plain synchronous methods.
    ///
    /// IMPORTANT: every instance method on this class (including the constructor, which calls
    /// Application.CreateApplication()) MUST be invoked from the same single STA thread for the
    /// lifetime of the instance - see EsapiStaThread. This class does no thread-marshaling
    /// itself; it trusts its caller (StaEsapiPatientService) to do that. Do not call it
    /// directly from a ViewModel, Task.Run, or any other thread.
    ///
    /// Written against the general shape of ESAPI v15/v16/v18. Three calls are known to vary
    /// between versions and must be checked against your own "Eclipse Scripting API Reference
    /// Guide" before clinical use - see README "Validating version-specific ESAPI behaviour":
    /// Patient.StructureSets (GetAllStructureSets below), the Image.GetVoxels buffer index
    /// order (ComputeMeanHu below), and the axis-aligned assumption ComputeMeanHu's containment
    /// test makes (see that method's remarks).
    /// </summary>
    internal sealed class EsapiPatientServiceCore : IDisposable
    {
        private readonly Application _app;
        private Patient _currentPatient;

        public EsapiPatientServiceCore()
        {
            // Standard entry point for a standalone (non-plugin) ESAPI executable; prompts for
            // Varian credentials interactively if the current Windows identity isn't already
            // authorised. If your site requires explicit credentials, use
            // Application.CreateApplication(string userId, SecureString password) instead.
            _app = Application.CreateApplication();
        }

        public IReadOnlyList<StructureSetSummary> OpenPatientAndGetStructureSets(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                throw new ArgumentException("Patient ID must not be blank.", nameof(patientId));

            ClosePatient();

            _currentPatient = _app.OpenPatientById(patientId.Trim());
            if (_currentPatient == null)
                throw new InvalidOperationException($"No patient found with ID '{patientId}'.");

            var sets = GetAllStructureSets(_currentPatient).ToList();
            if (sets.Count == 0)
                throw new InvalidOperationException("This patient has no structure sets.");

            return sets
                .Select(ss => new StructureSetSummary
                {
                    Id = ss.Id,
                    Description = $"{ss.Id}  (image: {ss.Image?.Id ?? "n/a"})"
                })
                .ToList();
        }

        public IReadOnlyList<StructureSummary> GetStructures(string structureSetId)
        {
            var structureSet = FindStructureSet(structureSetId);

            return structureSet.Structures
                .Select(s => new StructureSummary
                {
                    Id = s.Id,
                    DicomType = s.DicomType,
                    HasContours = s.HasSegment
                })
                .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public StructureStatistics ComputeStructureStatistics(string structureSetId, string structureId)
        {
            var structureSet = FindStructureSet(structureSetId);
            var structure = structureSet.Structures.FirstOrDefault(s => s.Id == structureId);
            if (structure == null)
                throw new InvalidOperationException($"Structure '{structureId}' not found in structure set '{structureSetId}'.");
            if (!structure.HasSegment)
                throw new InvalidOperationException($"Structure '{structureId}' has no contours on this image set.");

            var image = structureSet.Image;
            if (image == null)
                throw new InvalidOperationException("This structure set has no associated CT image.");

            double fovX = image.XRes * image.XSize;
            double fovY = image.YRes * image.YSize;

            var (meanHu, voxelCount) = ComputeMeanHu(structure, image);

            return new StructureStatistics
            {
                MeanHu = meanHu,
                VoxelCount = voxelCount,
                FovXMm = fovX,
                FovYMm = fovY,
                FovMm = (fovX + fovY) / 2.0
            };
        }

        public void ClosePatient()
        {
            if (_currentPatient != null)
            {
                _app.ClosePatient();
                _currentPatient = null;
            }
        }

        public void Dispose()
        {
            try
            {
                ClosePatient();
            }
            finally
            {
                _app?.Dispose();
            }
        }

        // --- Internals ---------------------------------------------------

        private StructureSet FindStructureSet(string structureSetId)
        {
            if (_currentPatient == null)
                throw new InvalidOperationException("No patient is currently open.");

            var structureSet = GetAllStructureSets(_currentPatient)
                .FirstOrDefault(ss => ss.Id == structureSetId);

            if (structureSet == null)
                throw new InvalidOperationException($"Structure set '{structureSetId}' not found for this patient.");

            return structureSet;
        }

        private static IEnumerable<StructureSet> GetAllStructureSets(Patient patient)
        {
            // Preferred path (ESAPI v15.6+): Patient exposes StructureSets directly. If this
            // property is unavailable in your version, replace this method body with a
            // Courses -> PlanSetups -> StructureSet traversal, de-duplicated by StructureSet.UID.
            return patient.StructureSets
                .Where(ss => ss != null)
                .GroupBy(ss => ss.UID)
                .Select(g => g.First());
        }

        /// <summary>
        /// Computes the mean HU of a structure by iterating CT voxels slice-by-slice, restricted
        /// to the structure's contour bounding box on each slice for performance, and testing
        /// containment with an in-process scanline (ray-casting) point-in-polygon test
        /// (https://en.wikipedia.org/wiki/Point_in_polygon) against the structure's own contour
        /// points, rather than calling the ESAPI/COM Structure.IsPointInsideSegment per pixel.
        /// Multiple contours on one slice (e.g. a structure with a hole) combine correctly under
        /// the even-odd rule used here, matching how DICOM RT contours are defined.
        ///
        /// Raw voxel values are converted to HU via Image.VoxelToDisplayValue, which applies the
        /// CT's own calibration curve - this is what allows the extended/metal HU range used in
        /// the reference tables, rather than the diagnostic -1000..3000 HU window. It is
        /// deliberately not short-circuited with a cached linear (slope/intercept) formula:
        /// extended/metal HU is exactly the range where that curve is most likely to be
        /// non-linear or piecewise. Don't add that shortcut without confirming linearity across
        /// your site's full HU range.
        ///
        /// Correctness note: converting contour points to pixel-index space via
        /// (point - origin) / resolution assumes the image's X/Y directions are axis-aligned
        /// (no gantry/couch tilt baked into the acquisition). Validate against known geometry
        /// (see class remarks) before clinical use.
        /// </summary>
        private static (double meanHu, long voxelCount) ComputeMeanHu(Structure structure, VMS.TPS.Common.Model.API.Image image)
        {
            double sum = 0;
            long count = 0;

            double xRes = image.XRes;
            double yRes = image.YRes;
            VVector origin = image.Origin;

            int xSize = image.XSize;
            int ySize = image.YSize;

            // Allocated once, reused every slice - GetVoxels overwrites it in place.
            var buffer = new int[xSize, ySize];

            for (int z = 0; z < image.ZSize; z++)
            {
                VVector[][] contours;
                try
                {
                    contours = structure.GetContoursOnImagePlane(z);
                }
                catch (Exception)
                {
                    continue;
                }

                if (contours == null || contours.Length == 0)
                    continue;

                // Convert every contour on this slice into pixel-index space up front, and
                // compute the mm bounding box from the same points in the same pass.
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                var pixelContours = new List<double[,]>(contours.Length);
                foreach (var contour in contours)
                {
                    if (contour == null || contour.Length < 3)
                        continue; // not a real polygon

                    var pts = new double[contour.Length, 2];
                    for (int p = 0; p < contour.Length; p++)
                    {
                        var pt = contour[p];
                        pts[p, 0] = (pt.x - origin.x) / xRes;
                        pts[p, 1] = (pt.y - origin.y) / yRes;

                        if (pt.x < minX) minX = pt.x;
                        if (pt.x > maxX) maxX = pt.x;
                        if (pt.y < minY) minY = pt.y;
                        if (pt.y > maxY) maxY = pt.y;
                    }
                    pixelContours.Add(pts);
                }

                if (pixelContours.Count == 0)
                    continue;

                // Map the mm bounding box to a pixel-index bounding box, with a 1-pixel margin
                // to avoid clipping edge voxels due to rounding.
                int iMin = Math.Max(0, (int)Math.Floor((minX - origin.x) / xRes) - 1);
                int iMax = Math.Min(xSize - 1, (int)Math.Ceiling((maxX - origin.x) / xRes) + 1);
                int jMin = Math.Max(0, (int)Math.Floor((minY - origin.y) / yRes) - 1);
                int jMax = Math.Min(ySize - 1, (int)Math.Ceiling((maxY - origin.y) / yRes) + 1);

                if (iMin > iMax || jMin > jMax)
                    continue;

                // Verify this buffer's index order against your ESAPI version (class remarks).
                // If mean HU comes out obviously wrong (e.g. background air values for a
                // structure you know is metal), swap the indices used to read `buffer` below.
                image.GetVoxels(z, buffer);

                var crossings = new List<double>();

                for (int j = jMin; j <= jMax; j++)
                {
                    double rowY = j;

                    crossings.Clear();
                    foreach (var pts in pixelContours)
                    {
                        int n = pts.GetLength(0);
                        for (int p = 0; p < n; p++)
                        {
                            double y0 = pts[p, 1];
                            double y1 = pts[(p + 1) % n, 1];
                            if (y0 == y1)
                                continue; // horizontal edge contributes no crossing

                            // Half-open interval [y0, y1) convention avoids double-counting a
                            // vertex that lies exactly on the scan line.
                            bool crosses = (rowY >= y0 && rowY < y1) || (rowY >= y1 && rowY < y0);
                            if (!crosses)
                                continue;

                            double x0 = pts[p, 0];
                            double x1 = pts[(p + 1) % n, 0];
                            double t = (rowY - y0) / (y1 - y0);
                            crossings.Add(x0 + t * (x1 - x0));
                        }
                    }

                    if (crossings.Count < 2)
                        continue;

                    crossings.Sort();

                    for (int k = 0; k + 1 < crossings.Count; k += 2)
                    {
                        int iStart = Math.Max(iMin, (int)Math.Ceiling(crossings[k]));
                        int iEnd = Math.Min(iMax, (int)Math.Floor(crossings[k + 1]));

                        for (int i = iStart; i <= iEnd; i++)
                        {
                            int rawVoxel = buffer[i, j];
                            double hu = image.VoxelToDisplayValue(rawVoxel);
                            sum += hu;
                            count++;
                        }
                    }
                }
            }

            if (count == 0)
                throw new InvalidOperationException(
                    "No voxels were found inside the selected structure - check that it is contoured " +
                    "on this image set and that it encloses a non-zero volume.");

            return (sum / count, count);
        }
    }
}