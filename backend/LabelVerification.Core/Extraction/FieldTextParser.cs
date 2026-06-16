using System.Text.RegularExpressions;
using LabelVerification.Core.Matching;
using LabelVerification.Core.Text;

namespace LabelVerification.Core.Extraction;

internal static class FieldTextParser
{
    private static readonly Regex TtbIdPattern = new(@"\b(\d{13,14})\b", RegexOptions.Compiled);
    private static readonly Regex AbvPattern = new(
        @"(\d{1,2}(?:[.,]\d{1,2})?)\s*%\s*(?:ALC\.?\s*/?\s*VOL\.?|BY\s*VOL\.?|ABV)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StatedAbvPattern = new(
        @"STATED\s+AL[EC][^0-9]{0,40}(\d{1,2}(?:[.,]\d{1,2})?)\s*%",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NetContentsPattern = new(
        @"(\d+(?:[.,]\d+)?)\s*(ML|M\s*L|CL|L|FL\s*OZ|FLOZ|OZ)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SizeFieldPattern = new(
        @"SIZE(?:\(S\))?\s*[:\-\s]+(\d+(?:\.\d+)?)\s*(ML|M\s*L|CL|L|FL\s*OZ|OZ)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImportedByPattern = new(
        @"IMPORTED\s+BY\s+([A-Z0-9][A-Z0-9\s,&.\-']{4,80})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DistilleryPattern = new(
        @"(?:DISTILLERY|DISTILLED\s+AND\s+BOTTLED\s+BY|BOTTLED\s+BY)[:\s]+([A-Z0-9][A-Z0-9\s,&.\-']{4,80})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] ClassKeywords =
    [
        "WHISKEY", "WHISKY", "TEQUILA", "GIN", "VODKA", "RUM", "BRANDY", "COGNAC",
        "RAICILLA", "MEZCAL", "BOURBON", "SCOTCH", "WINE", "BEER", "MALT BEVERAGE",
        "AGAVE SPIRIT", "SPIRITS",
    ];

    internal static string? ExtractTtbId(string text)
    {
        foreach (Match match in TtbIdPattern.Matches(text))
        {
            var digits = match.Groups[1].Value;
            if (digits.Length is 13 or 14)
            {
                return digits;
            }
        }

        return null;
    }

    internal static decimal? ExtractAbv(string text)
    {
        var stated = StatedAbvPattern.Match(text);
        if (stated.Success && decimal.TryParse(stated.Groups[1].Value.Replace(',', '.'), out var statedValue))
        {
            return statedValue;
        }

        decimal? best = null;
        var bestScore = double.MinValue;
        foreach (Match match in AbvPattern.Matches(text))
        {
            if (!decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), out var value) || value is < 3 or > 80)
            {
                continue;
            }

            var score = 1.0;
            var context = text.Substring(
                Math.Max(0, match.Index - 16),
                Math.Min(text.Length - Math.Max(0, match.Index - 16), match.Length + 32)).ToUpperInvariant();
            if (context.Contains("ALC", StringComparison.Ordinal))
            {
                score += 2;
            }

            if (context.Contains("STATED", StringComparison.Ordinal))
            {
                score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = value;
            }
        }

        return best;
    }

    internal static string? ExtractNetContents(string text)
    {
        var sizeField = SizeFieldPattern.Match(TextNormalizer.Normalize(text));
        if (sizeField.Success)
        {
            return NormalizeContents($"{sizeField.Groups[1].Value} {sizeField.Groups[2].Value}");
        }

        foreach (Match match in NetContentsPattern.Matches(text))
        {
            return NormalizeContents($"{match.Groups[1].Value} {match.Groups[2].Value}");
        }

        return null;
    }

    internal static string? ExtractBrand(string brandRegionText, string fullText)
    {
        var candidates = ExtractUppercaseLines(brandRegionText);
        if (candidates.Count == 0)
        {
            candidates = ExtractUppercaseLines(fullText);
        }

        return candidates
            .Where(line => line.Length >= 3 && !IsWarningOrCertificateLine(line))
            .OrderByDescending(line => line.Length)
            .FirstOrDefault();
    }

    internal static string? ExtractClassType(string classRegionText, string fullText)
    {
        var corpus = $"{classRegionText}\n{fullText}".ToUpperInvariant();
        foreach (var keyword in ClassKeywords.OrderByDescending(k => k.Length))
        {
            if (corpus.Contains(keyword, StringComparison.Ordinal))
            {
                return keyword;
            }
        }

        return ExtractUppercaseLines(classRegionText).FirstOrDefault(line => line.Length >= 4);
    }

    internal static string? ExtractBottlerAddress(string addressRegionText, string fullText)
    {
        var corpus = $"{addressRegionText}\n{fullText}";
        var imported = ImportedByPattern.Match(corpus);
        if (imported.Success)
        {
            return $"Imported by {CleanLine(imported.Groups[1].Value)}";
        }

        var distillery = DistilleryPattern.Match(corpus);
        if (distillery.Success)
        {
            return CleanLine(distillery.Groups[1].Value);
        }

        var lines = corpus
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(',', StringComparison.Ordinal) || line.Contains("LYNCHBURG", StringComparison.OrdinalIgnoreCase))
            .Select(CleanLine)
            .Where(line => line.Length >= 8)
            .ToList();

        return lines.FirstOrDefault();
    }

    internal static string? ExtractCountry(string countryRegionText, string fullText)
    {
        var corpus = TextNormalizer.Normalize($"{countryRegionText}\n{fullText}");
        if (corpus.Contains("HECHO EN MEXICO", StringComparison.Ordinal)
            || corpus.Contains("PRODUCT OF MEXICO", StringComparison.Ordinal))
        {
            return "Mexico";
        }

        if (corpus.Contains("UNITED STATES", StringComparison.Ordinal)
            || corpus.Contains(" USA", StringComparison.Ordinal)
            || corpus.Contains("U.S.A", StringComparison.Ordinal)
            || (corpus.Contains("LYNCHBURG", StringComparison.Ordinal) && corpus.Contains("TENN", StringComparison.Ordinal)))
        {
            return "United States";
        }

        if (corpus.Contains("MEXICO", StringComparison.Ordinal))
        {
            return "Mexico";
        }

        if (corpus.Contains("AUSTRALIA", StringComparison.Ordinal))
        {
            return "Australia";
        }

        return null;
    }

    internal static bool HasGovernmentWarning(string text) =>
        WarningTextHelper.ContainsRequiredWarningPhrases(WarningTextHelper.NormalizeTtbText(text));

    internal static string InferProductCategory(string? classType)
    {
        if (string.IsNullOrWhiteSpace(classType))
        {
            return "distilled_spirits";
        }

        var upper = classType.ToUpperInvariant();
        if (upper.Contains("WINE", StringComparison.Ordinal))
        {
            return "wine";
        }

        if (upper.Contains("BEER", StringComparison.Ordinal) || upper.Contains("MALT BEVERAGE", StringComparison.Ordinal))
        {
            return "beer";
        }

        return "distilled_spirits";
    }

    private static List<string> ExtractUppercaseLines(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanLine)
            .Where(line => line.Length >= 3 && line.Count(char.IsLetter) >= 3)
            .Where(line => line.Count(char.IsUpper) >= Math.Max(3, line.Count(char.IsLetter) / 2))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWarningOrCertificateLine(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.Contains("GOVERNMENT", StringComparison.Ordinal)
            || upper.Contains("WARNING", StringComparison.Ordinal)
            || upper.Contains("TTB NUMBER", StringComparison.Ordinal)
            || upper.Contains("CERTIFICATE", StringComparison.Ordinal)
            || upper.Contains("TEXAS ALCOHOLIC", StringComparison.Ordinal);
    }

    private static string CleanLine(string line) =>
        Regex.Replace(line.Trim(), @"\s{2,}", " ");

    private static string NormalizeContents(string value)
    {
        var normalized = TextNormalizer.Normalize(value).Replace("FLOZ", "FL OZ");
        var match = NetContentsPattern.Match(normalized);
        if (!match.Success)
        {
            return normalized;
        }

        var unit = match.Groups[2].Value.ToUpperInvariant().Replace(" ", "") switch
        {
            "ML" => "mL",
            "CL" => "CL",
            "L" => "L",
            "FLOZ" or "OZ" => "FL OZ",
            _ => "mL",
        };

        return $"{match.Groups[1].Value} {unit}";
    }
}
