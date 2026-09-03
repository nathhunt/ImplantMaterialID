namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Voxel-derived statistics for a structure, computed from the CT associated with its
    /// structure set.
    /// </summary>
    public class StructureStatistics
    {
        public double MeanHu { get; set; }
        public long VoxelCount { get; set; }

        /// <summary>Reconstruction FOV in mm, averaged from image X/Y resolution * matrix size.</summary>
        public double FovMm { get; set; }

        /// <summary>FOV computed from the X axis alone (XRes * XSize), for the non-square check.</summary>
        public double FovXMm { get; set; }

        /// <summary>FOV computed from the Y axis alone (YRes * YSize), for the non-square check.</summary>
        public double FovYMm { get; set; }
    }
}
