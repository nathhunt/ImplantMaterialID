namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Reference data transcribed from the site's scanner characterisation tables (mean HU of
    /// a cylindrical implant at a given diameter and FOV, for Stainless Steel and Titanium).
    /// Regenerate these arrays for your own scanner/reconstruction kernel/HU calibration -
    /// results are only as good as the source tables they were copied from.
    ///
    /// Grid layout: rows = Diameter (mm), columns = FOV (mm). Both axes are stored in
    /// ASCENDING order, as required by <see cref="BilinearInterpolator"/>.
    /// </summary>
    public static class MaterialReferenceData
    {
        /// <summary>Diameters (mm), ascending. Must match the row order of both HU grids.</summary>
        public static readonly double[] Diameters = { 4, 6, 8, 10, 12, 14, 16 };

        /// <summary>Reconstruction FOV (mm), ascending. Must match the column order of both HU grids.</summary>
        public static readonly double[] FovsMm = { 400, 550, 700 };

        /// <summary>
        /// Stainless steel mean HU, [diameterIndex, fovIndex]. Columns are reordered to
        /// ascending FOV (400/550/700) from the source table's descending order.
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
        /// Titanium mean HU, [diameterIndex, fovIndex]. Same column reordering as
        /// <see cref="StainlessSteelHu"/>.
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
