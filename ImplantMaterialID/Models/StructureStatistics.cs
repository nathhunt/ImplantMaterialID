namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Voxel-derived statistics for a structure, computed from the CT associated with its
    /// structure set.
    /// </summary>
    public class StructureStatistics
    {
        /// <summary>70th/80th/90th percentile HU of the voxels inside the structure (see <see cref="Percentile.Linear"/>).</summary>
        public double P70Hu { get; set; }
        public double P80Hu { get; set; }
        public double P90Hu { get; set; }

        public long VoxelCount { get; set; }

        /// <summary>In-plane pixel size in mm, averaged from image X/Y resolution.</summary>
        public double PixelSizeMm { get; set; }

        /// <summary>X and Y resolution separately, for the non-square-pixel check.</summary>
        public double PixelSizeXMm { get; set; }
        public double PixelSizeYMm { get; set; }

        /// <summary>Reconstruction FOV in mm (pixel size * matrix size), shown for reference only.</summary>
        public double FovMm { get; set; }
    }
}
