namespace ImplantMaterialID.Models
{
    /// <summary>
    /// Display-friendly summary of an ESAPI StructureSet. Kept separate from the live
    /// VMS.TPS.Common.Model.API.StructureSet object so the ViewModel/View layers don't need
    /// a reference to ESAPI types (and so the underlying patient context can be closed/reopened
    /// safely by the service layer).
    /// </summary>
    public class StructureSetSummary
    {
        public string Id { get; set; }

        /// <summary>Human-readable extra context, e.g. associated study/plan, shown in the combo box.</summary>
        public string Description { get; set; }

        public override string ToString() => Description;
    }
}
