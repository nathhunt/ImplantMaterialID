namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Static reference data transcribed from the site's own scanner characterisation tables
    /// (mean HU of a cylindrical implant of a given diameter, reconstructed at a given FOV,
    /// for Stainless Steel and Titanium). These MUST be regenerated for your own scanner /
    /// reconstruction kernel / extended-HU calibration curve - the numbers here are only as
    /// good as the two source tables they were copied from.
    ///
    /// Grid layout: rows = Diameter (mm), columns = FOV (mm).
    /// Both axes are stored in ASCENDING order (required by <see cref="BilinearInterpolator"/>),
    /// even though the source tables listed FOV as 700 / 550 / 400 (descending).
    /// </summary>
    public static class MaterialReferenceData
    {
        /// <summary>Diameters (mm), ascending. Must match the row order of both HU grids.</summary>
        public static readonly double[] Diameters = { 4, 6, 8, 10, 12, 14, 16 };

        /// <summary>Reconstruction FOV (mm), ascending. Must match the column order of both HU grids.</summary>
        public static readonly double[] FovsMm = { 400, 550, 700 };

        /// <summary>
        /// Stainless steel mean HU, [diameterIndex, fovIndex].
        /// Source table (Dia / 700 / 550 / 400):
        ///   4  13243 21325 23983
        ///   6  15691 20786 21824
        ///   8  16093 19587 20034
        ///  10  16040 18218 18632
        ///  12  15349 17197 17476
        ///  14  14964 16230 16530
        ///  16  13763 14773 14989
        /// </summary>
        public static readonly double[,] StainlessSteelHu =
        {
            /* Dia=4  FOV400,550,700 */ { 23983, 21325, 13243 },
            /* Dia=6                */ { 21824, 20786, 15691 },
            /* Dia=8                */ { 20034, 19587, 16093 },
            /* Dia=10               */ { 18632, 18218, 16040 },
            /* Dia=12               */ { 17476, 17197, 15349 },
            /* Dia=14               */ { 16530, 16230, 14964 },
            /* Dia=16               */ { 14989, 14773, 13763 },
        };

        /// <summary>
        /// Titanium mean HU, [diameterIndex, fovIndex].
        /// Source table (Dia / 700 / 550 / 400):
        ///   4  7075 11393 12812
        ///   6  8383 11104 11659
        ///   8  8597 10464 10703
        ///  10  8569  9733  9954
        ///  12  8200  9187  9336
        ///  14  7994  8670  8831
        ///  16  7352  7892  8007
        /// </summary>
        public static readonly double[,] TitaniumHu =
        {
            /* Dia=4  FOV400,550,700 */ { 12812, 11393, 7075 },
            /* Dia=6                */ { 11659, 11104, 8383 },
            /* Dia=8                */ { 10703, 10464, 8597 },
            /* Dia=10               */ { 9954, 9733, 8569 },
            /* Dia=12               */ { 9336, 9187, 8200 },
            /* Dia=14               */ { 8831, 8670, 7994 },
            /* Dia=16               */ { 8007, 7892, 7352 },
        };
    }
}
