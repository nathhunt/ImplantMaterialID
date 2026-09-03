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
    /// IMPORTANT - version-dependent API surface:
    /// This class was written against the general shape of ESAPI v15/v16/v18. A few calls are
    /// known to vary between ESAPI versions and MUST be checked against your own version's
    /// "Eclipse Scripting API Reference Guide" PDF before clinical use:
    ///   1. Patient.StructureSets - added as a direct convenience property in later ESAPI
    ///      versions. If your version doesn't have it, replace GetAllStructureSets() below
    ///      with a traversal via patient.Courses -> course.PlanSetups -> plan.StructureSet
    ///      (de-duplicated by StructureSet.Id/UID).
    ///   2. Image.GetVoxels(int z, int[,] buffer) - the exact buffer index order
    ///      ([x, y] vs [y, x]) has differed across API versions. Validate this on a structure
    ///      of known, uniform composition (e.g. a water-equivalent phantom insert) before
    ///      trusting the mean HU numbers clinically - see README "Validating the voxel loop".
    ///   3. ComputeMeanHu's containment test assumes the image's X/Y directions are axis-
    ///      aligned (no gantry/couch tilt baked into the acquisition) - see the method's own
    ///      remarks below. This matches the assumption the bounding-box crop already made,
    ///      but is now load-bearing for correctness, not just for cropping, so validate it on
    ///      the same phantom used for check #2.
    /// </summary>
    internal sealed class EsapiPatientServiceCore : IDisposable
    {
        private readonly Application _app;
        private Patient _currentPatient;

        public EsapiPatientServiceCore()
        {
            // Parameterless CreateApplication() is the standard entry point for a standalone
            // (non-plugin) ESAPI executable; it will prompt for Varian credentials interactively
            // if the current Windows identity isn't already authorised. If your site instead
            // requires explicit credentials, use the
            // Application.CreateApplication(string userId, System.Security.SecureString password)
            // overload here instead.
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
            // Preferred path (ESAPI v15.6+): Patient exposes StructureSets directly.
            // If this property is unavailable in your ESAPI version, replace this method body
            // with the Courses -> PlanSetups -> StructureSet traversal described in the class
            // remarks above.
            return patient.StructureSets
                .Where(ss => ss != null)
                .GroupBy(ss => ss.UID)
                .Select(g => g.First());
        }

        /// <summary>
        /// Computes the mean HU of a structure by iterating CT voxels slice-by-slice, restricted
        /// to the structure's contour bounding box on each slice for performance, and testing
        /// containment with an in-process scanline (ray-casting) point-in-polygon test against
        /// the structure's own contour points. Raw voxel values are converted to HU via
        /// Image.VoxelToDisplayValue, which applies the CT's own calibration curve (this is what
        /// allows the extended/metal HU range used in the reference tables, rather than the
        /// diagnostic -1000..3000 HU window).
        ///
        /// Performance notes (see git history / PR description for the "before" version):
        ///   - The voxel buffer is allocated once and reused across slices via GetVoxels,
        ///     instead of being re-allocated on every slice that has contours.
        ///   - Containment is no longer tested by calling Structure.IsPointInsideSegment once
        ///     per pixel in the bounding box. That call crosses into ESAPI/COM, and the old code
        ///     paid that interop cost for every pixel in the box regardless of whether it was
        ///     anywhere near the structure. Instead, each slice's contours are converted to
        ///     pixel-index coordinates once, and a standard scanline point-in-polygon test
        ///     (https://en.wikipedia.org/wiki/Point_in_polygon) determines the included x-range
        ///     directly for each row. Multiple contours on one slice (e.g. a structure with a
        ///     hole) combine correctly under the even-odd rule used here, which is the same rule
        ///     DICOM RT contours are defined against - no special-casing needed for holes.
        ///   - VoxelToDisplayValue is still called once per *accepted* voxel (not per candidate
        ///     voxel), so its cost now scales with the structure's actual area rather than its
        ///     bounding box. It is deliberately NOT short-circuited with a cached linear
        ///     (slope/intercept) formula, even though that's a common CT-HU pattern: this
        ///     project's whole point is extended/metal HU handling, which is exactly the case
        ///     where the calibration curve is most likely to be non-linear or piecewise. Don't
        ///     add that shortcut without confirming linearity across your full site's HU range.
        ///
        /// Correctness note: converting contour points to pixel-index space via
        /// (point - origin) / resolution assumes the image's X/Y directions are axis-aligned
        /// (no tilt). The original bounding-box crop already made this same assumption for
        /// selecting which pixels to test; this version extends it to the containment test
        /// itself, so it's now load-bearing rather than just an optimization for the crop
        /// window. Validate against known geometry (see class remarks) before clinical use.
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

            // Allocated once, reused every slice - GetVoxels overwrites it in place, so there's
            // no need to allocate a fresh (potentially ~1 MB+) array per slice.
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

                // NOTE: verify this buffer's index order against your ESAPI version - see the
                // class-level remarks. If mean HU comes out obviously wrong (e.g. background air
                // values for a structure you know is metal), the fix is almost always to swap the
                // two indices used to read `buffer` below (and its allocation dimensions).
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