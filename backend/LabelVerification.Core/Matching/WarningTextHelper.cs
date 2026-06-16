namespace LabelVerification.Core.Matching;

using LabelVerification.Core.Text;

internal static class WarningTextHelper
{
    internal static string NormalizeTtbText(string text)
    {
        var normalized = TextNormalizer.Normalize(text);
        return normalized
            .Replace('{', '(')
            .Replace('}', ')')
            .Replace("(", " (")
            .Replace("  ", " ")
            .Trim();
    }

    internal static bool ContainsRequiredWarningPhrases(string normalizedOcr)
    {
        var hits = 0;
        if (normalizedOcr.Contains("GOVERNMENT WARNING", StringComparison.Ordinal)
            || normalizedOcr.Contains("GOVERNMENT WARKING", StringComparison.Ordinal)
            || normalizedOcr.Contains("GOVERNMENT IVARKING", StringComparison.Ordinal)
            || normalizedOcr.Contains("GOVRNMENT WARNING", StringComparison.Ordinal)
            || normalizedOcr.Contains("AGOVRNMENT IVARKING", StringComparison.Ordinal)
            || (normalizedOcr.Contains("GOVERNMENT", StringComparison.Ordinal)
                && (normalizedOcr.Contains("WARNING", StringComparison.Ordinal)
                    || normalizedOcr.Contains("WARKING", StringComparison.Ordinal)
                    || normalizedOcr.Contains("IVARKING", StringComparison.Ordinal)))
            || ((normalizedOcr.Contains("GOVRNMENT", StringComparison.Ordinal)
                    || normalizedOcr.Contains("AGOVRNMENT", StringComparison.Ordinal))
                && (normalizedOcr.Contains("WARNING", StringComparison.Ordinal)
                    || normalizedOcr.Contains("WARKING", StringComparison.Ordinal)
                    || normalizedOcr.Contains("IVARKING", StringComparison.Ordinal))))
        {
            hits++;
        }

        if (normalizedOcr.Contains("SURGEON GENERAL", StringComparison.Ordinal)
            || normalizedOcr.Contains("SURGEON GEN", StringComparison.Ordinal)
            || normalizedOcr.Contains("ACCORDING TO THE SURGEON", StringComparison.Ordinal)
            || ContainsSurgeonGeneralPhrase(normalizedOcr))
        {
            hits++;
        }

        if (normalizedOcr.Contains("PREGNANCY", StringComparison.Ordinal)
            || normalizedOcr.Contains("PROGNANCY", StringComparison.Ordinal)
            || normalizedOcr.Contains("FRECNANCY", StringComparison.Ordinal)
            || normalizedOcr.Contains("FRECRLINCY", StringComparison.Ordinal)
            || normalizedOcr.Contains("BIRTH DEFECTS", StringComparison.Ordinal)
            || normalizedOcr.Contains("BRTH DEFECTS", StringComparison.Ordinal)
            || ContainsPregnancyPhrase(normalizedOcr))
        {
            hits++;
        }

        if (normalizedOcr.Contains("IMPAIRS YOUR", StringComparison.Ordinal)
            || normalizedOcr.Contains("IMPAIRS", StringComparison.Ordinal)
            || normalizedOcr.Contains("MPAIRS YOUR", StringComparison.Ordinal)
            || normalizedOcr.Contains("INPAIRS", StringComparison.Ordinal)
            || normalizedOcr.Contains("INPAINS", StringComparison.Ordinal)
            || normalizedOcr.Contains("ONPAIRS", StringComparison.Ordinal)
            || normalizedOcr.Contains("IMPAIBS", StringComparison.Ordinal))
        {
            hits++;
        }

        if (normalizedOcr.Contains("OPERATE MACHINERY", StringComparison.Ordinal)
            || (normalizedOcr.Contains("OPERATE", StringComparison.Ordinal)
                && normalizedOcr.Contains("MACHINERY", StringComparison.Ordinal))
            || (normalizedOcr.Contains("MACHIN", StringComparison.Ordinal)
                && normalizedOcr.Contains("DRIVE", StringComparison.Ordinal))
            || (normalizedOcr.Contains("DRIYE", StringComparison.Ordinal)
                && normalizedOcr.Contains("CAR", StringComparison.Ordinal)))
        {
            hits++;
        }

        if (normalizedOcr.Contains("ALCOHOLIC BEVERAGES", StringComparison.Ordinal))
        {
            hits++;
        }

        if (hits >= 2)
        {
            return true;
        }

        var warningTokenHits = new[]
            {
                "GOVERNMENT", "GOVRNMENT", "AGOVRNMENT", "G0VERNMENT", "WARNING", "WARKING", "IVARKING", "IVARNINGS",
                "SURGEON", "GENERAL", "PREGNAN", "BIRTH DEFECT", "IMPAIR", "OPERATE", "MACHINERY", "ALCOHOLIC BEVERAGES",
                "CONSUMPTION", "DEFECTS",
            }
            .Count(token => normalizedOcr.Contains(token, StringComparison.Ordinal));

        if (normalizedOcr.Contains("ALCOHOLIC BEVERAGES", StringComparison.Ordinal)
            && warningTokenHits >= 2)
        {
            return true;
        }

        return warningTokenHits >= 3;
    }

    private static bool ContainsSurgeonGeneralPhrase(string normalizedOcr)
    {
        if (!normalizedOcr.Contains("GENERAL", StringComparison.Ordinal))
        {
            return false;
        }

        string[] surgeonTokens =
        [
            "SURGEON", "SUNGEON", "SURGEDN", "SSRGEON", "SSRGEDH", "SURGEON GENERAL",
        ];

        return surgeonTokens.Any(token => normalizedOcr.Contains(token, StringComparison.Ordinal));
    }

    private static bool ContainsPregnancyPhrase(string normalizedOcr)
    {
        if (normalizedOcr.Contains("PREG", StringComparison.Ordinal))
        {
            return true;
        }

        string[] pregnancyTokens = ["NANGY", "NANEY", "NANCY", "PREGNAN", "PREG-"];
        return pregnancyTokens.Any(token => normalizedOcr.Contains(token, StringComparison.Ordinal));
    }
}
