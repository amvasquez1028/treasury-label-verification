namespace LabelVerification.Core.Matching;

internal static class FieldConfidenceCommentary
{
    internal static string? ForWarningField(string fieldName, bool isMatch, double confidence, string? baseNotes)
    {
        if (isMatch)
        {
            return baseNotes;
        }

        var band = DescribeBand(confidence);
        var categoryHint = fieldName switch
        {
            "TtbWarningText" =>
                "Government warning body text was not fully recovered by OCR on this flat label page.",
            "BoldWarningPhrase" =>
                "The bold GOVERNMENT WARNING heading was not clearly detected in OCR.",
            "WarningPlacement" =>
                "Could not confirm the government warning sits in the lower label area from OCR alone.",
            "BoldWarningTypography" =>
                "Heading stroke weight could not be confirmed as materially bolder than body warning text.",
            "WarningContrast" =>
                "Warning band contrast fell below the minimum readability threshold.",
            _ => "Warning-related field did not meet the verification threshold.",
        };

        var guidance =
            confidence < 0.6
                ? " Low confidence — review the warning page by eye or request a higher-resolution scan."
                : " Review recommended — partial OCR or layout signal; expand field details for expected vs extracted.";

        return string.IsNullOrWhiteSpace(baseNotes)
            ? $"{categoryHint} Confidence {band}.{guidance}"
            : $"{baseNotes} Confidence {band}.{guidance}";
    }

    internal static string DescribeBand(double confidence)
    {
        var percent = (int)Math.Round(confidence * 100);
        return confidence switch
        {
            >= 0.9 => $"{percent}% (high — green band)",
            >= 0.6 => $"{percent}% (moderate — yellow band; outcome still fails until matched)",
            _ => $"{percent}% (low — red band)",
        };
    }
}
