using System.Text.RegularExpressions;
using LabelVerification.Core.Models;
using LabelVerification.Core.Text;

namespace LabelVerification.Core.Matching;

internal static class NameMatchHelper
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "AND", "OR", "THE", "A", "AN", "OF", "BY", "WITH",
    };

    public static double MatchConfidence(string ocrText, string expectedName)
    {
        var normalizedOcr = TextNormalizer.Normalize(ocrText);
        var expected = TextNormalizer.Normalize(expectedName);
        if (expected.Length == 0)
        {
            return 0;
        }

        if (normalizedOcr.Contains(expected, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var tokens = SignificantNameTokens(expected);
        if (tokens.Count == 0)
        {
            return 0;
        }

        return tokens.Average(token =>
            normalizedOcr.Contains(token, StringComparison.Ordinal)
                ? 1.0
                : TextNormalizer.SimilarityRatio(normalizedOcr, token));
    }

    public static IReadOnlyList<string> SignificantNameTokens(string normalizedName) =>
        normalizedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2 && !StopWords.Contains(token))
            .ToArray();

    public static bool IsSignificantAddressToken(string token)
    {
        if (token.Length > 2)
        {
            return true;
        }

        if (token.Length != 2)
        {
            return false;
        }

        return token.All(char.IsLetter);
    }

    public static bool ShouldFallbackToServerOcr(string clientText, ExpectedLabelFields expected)
    {
        if (string.IsNullOrWhiteSpace(clientText))
        {
            return true;
        }

        var brandConf = MatchConfidence(clientText, expected.BrandName);
        if (!string.IsNullOrWhiteSpace(expected.FancifulName))
        {
            brandConf = Math.Max(brandConf, MatchConfidence(clientText, expected.FancifulName));
        }

        var classConf = MatchConfidence(clientText, expected.ClassTypeDesignation);
        var abvFound = AbvExtraction.TryExtract(clientText, expected.ProductCategory, out _);

        if (brandConf >= 0.35 || classConf >= 0.35 || abvFound)
        {
            return false;
        }

        return true;
    }
}

internal static class AbvExtraction
{
    public static bool TryExtract(string text, string productCategory, out decimal value)
    {
        value = 0;
        string[] patterns =
        [
            @"(\d{1,2}(?:\.\d{1,2})?)\s*%\s*(?:ALC\.?\s*BY\s*VOL\.?|ABV)?",
            @"(\d{1,2}(?:\.\d{1,2})?)\s*%"
        ];

        if (productCategory is "wine" or "beer")
        {
            patterns =
            [
                @"(\d{1,2}(?:\.\d{1,2})?)\s*%\s*(?:ALC\.?\s*BY\s*VOL\.?|ABV)?",
                @"ALC\.?\s*(\d{1,2}(?:\.\d{1,2})?)\s*%\s*(?:BY\s*VOL\.?)?",
                @"(\d{1,2}(?:\.\d{1,2})?)\s*%\s*ALC",
                @"(\d{1,2}(?:\.\d{1,2})?)\s*%"
            ];
        }

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out value))
            {
                return true;
            }
        }

        return false;
    }
}
