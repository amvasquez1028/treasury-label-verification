using LabelVerification.Core.Matching;

namespace LabelVerification.Tests;

public class WarningTextHelperTests
{
    [Theory]
    [InlineData("AGOVRNMENT IVARKING ACCORDING TO THE SURGEON GENERAL")]
    [InlineData("GOVRNMENT WARKING (1) WOMEN SHOULD NOT DRINK ALCOHOLIC BEVERAGES")]
    [InlineData("SURGEON GENERAL BIRTH DEFECTS ALCOHOLIC BEVERAGES")]
    public void ContainsRequiredWarningPhrases_accepts_garbled_production_tokens(string ocr)
    {
        var normalized = WarningTextHelper.NormalizeTtbText(ocr);
        Assert.True(WarningTextHelper.ContainsRequiredWarningPhrases(normalized));
    }

    [Fact]
    public void ContainsRequiredWarningPhrases_rejects_unrelated_text()
    {
        var normalized = WarningTextHelper.NormalizeTtbText("LA VENENOSA RAICILLA 750 ML JALISCO");
        Assert.False(WarningTextHelper.ContainsRequiredWarningPhrases(normalized));
    }
}
