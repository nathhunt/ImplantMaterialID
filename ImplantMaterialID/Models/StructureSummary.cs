namespace ImplantMaterialID.Models
{
    public class StructureSummary
    {
        public string Id { get; set; }
        public string DicomType { get; set; }
        public bool HasContours { get; set; }

        public override string ToString() =>
            HasContours ? Id : $"{Id} (no contours on this image)";
    }
}
