using ImplantMaterialID.Models;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class MaterialClassificationResultTests
    {
        private static MaterialClassificationResult Result(bool agree, LikelyMaterial final, params string[] warnings)
        {
            var r = new MaterialClassificationResult { ClassifiersAgree = agree, FinalMaterial = final };
            r.Warnings.AddRange(warnings);
            return r;
        }

        [Fact]
        public void AgreeWithoutWarnings_IsHighConfidence()
        {
            var r = Result(true, LikelyMaterial.Titanium);

            Assert.Equal(ResultConfidence.High, r.Confidence);
            Assert.False(r.HasWarnings);
            Assert.Equal("Titanium", r.Headline);
            Assert.Equal("Both checks agree", r.ConfidenceLabel);
            Assert.Contains("titanium", r.Explanation);
        }

        [Fact]
        public void AgreeWithWarnings_IsReviewRecommended()
        {
            var r = Result(true, LikelyMaterial.StainlessSteel, "small contour");

            Assert.Equal(ResultConfidence.ReviewRecommended, r.Confidence);
            Assert.True(r.HasWarnings);
            Assert.Equal("Stainless steel", r.Headline);
            Assert.Equal("Both checks agree - result flagged", r.ConfidenceLabel);
            Assert.Contains("stainless steel", r.Explanation);
        }

        [Fact]
        public void Disagree_IsInconclusiveRegardlessOfWarnings()
        {
            var r = Result(false, LikelyMaterial.Indeterminate);

            Assert.Equal(ResultConfidence.Inconclusive, r.Confidence);
            Assert.Equal("Unable to determine", r.Headline);
            Assert.Equal("The two checks disagree", r.ConfidenceLabel);
            Assert.Contains("different answers", r.Explanation);
        }

        [Theory]
        [InlineData(LikelyMaterial.StainlessSteel, "Stainless steel")]
        [InlineData(LikelyMaterial.Titanium, "Titanium")]
        [InlineData(LikelyMaterial.Indeterminate, "Indeterminate")]
        public void Display_GivesPlainLanguageName(LikelyMaterial material, string expected)
        {
            Assert.Equal(expected, MaterialClassificationResult.Display(material));
        }

        [Fact]
        public void VerdictTexts_UseDisplayNames()
        {
            var r = new MaterialClassificationResult
            {
                GlobalVerdict = LikelyMaterial.StainlessSteel,
                MatchedVerdict = LikelyMaterial.Titanium,
            };

            Assert.Equal("Stainless steel", r.GlobalVerdictText);
            Assert.Equal("Titanium", r.MatchedVerdictText);
        }
    }
}
