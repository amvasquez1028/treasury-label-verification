using System.Text.RegularExpressions;
using LabelVerification.Core.Matching;

namespace LabelVerification.Core.Ocr;

/// <summary>
/// Determines whether merged OCR text already contains the TTB fields submission-grade flats need.
/// </summary>
internal static partial class FlatArtworkOcrSufficiency
{
    private static readonly Regex StatedAbv = StatedAbvPattern();
    private static readonly Regex CertificateAbv = CertificateAbvPattern();
    private static readonly Regex CertSize = CertSizePattern();
    private static readonly Regex CertificateContext = CertificateContextPattern();
    private static readonly Regex NetContents = NetContentsRegex();

    internal static bool HasWarningCorpus(string corpus) =>
        WarningTextHelper.ContainsRequiredWarningPhrases(WarningTextHelper.NormalizeTtbText(corpus));

    internal static bool HasAbvCorpus(string corpus)
    {
        var normalized = WarningTextHelper.NormalizeTtbText(corpus);
        if (StatedAbv.IsMatch(normalized))
        {
            return true;
        }

        return CertificateAbv.IsMatch(normalized);
    }

    internal static bool HasNetContentsCorpus(string corpus)
    {
        var normalized = WarningTextHelper.NormalizeTtbText(corpus);
        if (CertSize.IsMatch(normalized))
        {
            return true;
        }

        return CertificateContext.IsMatch(normalized)
            && NetContents.IsMatch(normalized);
    }

    internal static bool IsSubmissionCorpusComplete(string corpus)
    {
        if (string.IsNullOrWhiteSpace(corpus))
        {
            return false;
        }

        if (!HasWarningCorpus(corpus))
        {
            return false;
        }

        if (!HasAbvCorpus(corpus))
        {
            return false;
        }

        if (!HasNetContentsCorpus(corpus))
        {
            return false;
        }

        var normalized = WarningTextHelper.NormalizeTtbText(corpus);
        var substantiveWords = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Count(word => word.Length >= 3);

        return substantiveWords >= 8;
    }

    internal static bool NeedsCertificateMetadata(string corpus) =>
        !HasAbvCorpus(corpus) || !HasNetContentsCorpus(corpus);

    [GeneratedRegex(@"STATED\s+AL[EC][^0-9]{0,40}\d{1,2}(?:\.\d)?\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex StatedAbvPattern();

    [GeneratedRegex(@"\b\d{1,2}(?:\.\d)?\s*%\s*(?:ALC\.?\s*/?\s*VOL|BY\s*VOL|ABV)", RegexOptions.IgnoreCase)]
    private static partial Regex CertificateAbvPattern();

    [GeneratedRegex(@"SIZE(?:\(S\))?\s*[:\-\s]+\d+(?:\.\d+)?\s*(?:ML|M\s*L|CL|L|FL\s*OZ|OZ)", RegexOptions.IgnoreCase)]
    private static partial Regex CertSizePattern();

    [GeneratedRegex(@"STATED\s+AL|SIZE(?:\(S\))?|ALCOHOLIC\s+BEVERAGE\s+COMMISSION", RegexOptions.IgnoreCase)]
    private static partial Regex CertificateContextPattern();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\s*(?:ML|L|LITERS?|FL\s*OZ|OZ)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NetContentsRegex();
}
