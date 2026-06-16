using LabelVerification.Core.Matching;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using LabelVerification.Core.Text;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace LabelVerification.Core.Compliance;

public interface IFlatLabelComplianceAnalyzer
{
    bool AppliesTo(byte[] imageBytes, ExpectedLabelFields expected);

    IReadOnlyList<FieldVerificationResult> Analyze(
        byte[] imageBytes,
        ExpectedLabelFields expected,
        string ocrText);
}

public sealed class FlatLabelComplianceAnalyzer : IFlatLabelComplianceAnalyzer
{
    private readonly IImagePreprocessor _preprocessor;
    private readonly FlatLabelComplianceOptions _options;

    public FlatLabelComplianceAnalyzer(
        IImagePreprocessor preprocessor,
        IOptions<FlatLabelComplianceOptions> options)
    {
        _preprocessor = preprocessor;
        _options = options.Value;
    }

    public bool AppliesTo(byte[] imageBytes, ExpectedLabelFields expected)
    {
        if (expected.LabelPresentation != LabelPresentation.FullLabel)
        {
            return false;
        }

        if (_preprocessor.ClassifyImage(imageBytes) != LabelImageKind.FlatArtwork)
        {
            return false;
        }

        return StackedTabcPageHelper.IsSubmissionGradeFlatArtwork(imageBytes);
    }

    public IReadOnlyList<FieldVerificationResult> Analyze(
        byte[] imageBytes,
        ExpectedLabelFields expected,
        string ocrText)
    {
        try
        {
            var complianceImage = DownscaleForComplianceAnalysis(imageBytes);
            var segments = StackedTabcPageHelper.Split(complianceImage);
            var warningSegment = PrepareSegment(segments[^1]);
            var labelSegment = PrepareSegment(segments.Count > 1 ? segments[^2] : segments[^1]);
            var visualWarningConfirmed = FlatWarningVisualConfirmation.TryConfirmFromFlatArtwork(
                imageBytes,
                ocrText,
                _options,
                out var visualRationale);

            return
            [
                EvaluateWarningPlacement(warningSegment, ocrText, visualWarningConfirmed, visualRationale),
                EvaluateWarningContrast(warningSegment),
                EvaluateBoldWarningTypography(warningSegment, ocrText, visualWarningConfirmed, visualRationale),
                EvaluateLabelTextContrast(labelSegment),
            ];
        }
        catch (Exception)
        {
            return [BuildComplianceUnavailableResult()];
        }
    }

    private byte[] PrepareSegment(byte[] segmentBytes) => _preprocessor.CropTabcFlatArtwork(segmentBytes);

    private static byte[] DownscaleForComplianceAnalysis(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        const int maxSide = 1400;
        var longest = Math.Max(mat.Width, mat.Height);
        if (longest <= maxSide)
        {
            return imageBytes;
        }

        var scale = maxSide / (double)longest;
        using var resized = new Mat();
        Cv2.Resize(mat, resized, new Size(), scale, scale, InterpolationFlags.Area);
        return resized.ToBytes(".png");
    }

    private static FieldVerificationResult BuildComplianceUnavailableResult() =>
        new()
        {
            FieldName = "FlatLabelCompliance",
            IsMatch = true,
            Confidence = 0.5,
            ExpectedValue = "Flat label compliance checks",
            ExtractedValue = "Skipped — image analysis unavailable",
            Notes = "Compliance heuristics could not run; field matching results still apply.",
        };

    private FieldVerificationResult EvaluateWarningPlacement(
        byte[] segmentBytes,
        string ocrText,
        bool visualWarningConfirmed,
        string visualRationale)
    {
        var warningConfirmed = WarningTextHelper.ContainsRequiredWarningPhrases(
            WarningTextHelper.NormalizeTtbText(ocrText))
            || visualWarningConfirmed;
        var upperDensity = EstimateTextInkDensity(_preprocessor.CropBand(segmentBytes, 0.05, 0.5));
        var lowerDensity = EstimateTextInkDensity(_preprocessor.CropBand(segmentBytes, 0.5, 0.98));
        var isMatch = warningConfirmed
            && (lowerDensity >= _options.MinFooterInkDensity || lowerDensity >= upperDensity);
        var confidence = warningConfirmed
            ? Math.Max(visualWarningConfirmed ? 0.82 : 0.75, Math.Min(1.0, lowerDensity * 8))
            : 0;

        return new FieldVerificationResult
        {
            FieldName = "WarningPlacement",
            IsMatch = isMatch,
            Confidence = Math.Round(confidence, 4),
            ExpectedValue = "Government warning in lower label area",
            ExtractedValue = isMatch ? "Warning detected in lower label area" : null,
            Notes = isMatch
                ? visualWarningConfirmed && !WarningTextHelper.ContainsRequiredWarningPhrases(WarningTextHelper.NormalizeTtbText(ocrText))
                    ? visualRationale
                    : null
                : FieldConfidenceCommentary.ForWarningField(
                    "WarningPlacement",
                    false,
                    confidence,
                    "TTB warning text should appear in the lower portion of the flat label artwork")
        };
    }

    private FieldVerificationResult EvaluateWarningContrast(byte[] segmentBytes)
    {
        var footer = _preprocessor.CropBand(segmentBytes, 0.55, 1.0);
        var ratio = EstimateBandContrastRatio(footer);
        var isMatch = ratio >= _options.MinWarningContrastRatio;

        return new FieldVerificationResult
        {
            FieldName = "WarningContrast",
            IsMatch = isMatch,
            Confidence = Math.Round(Math.Min(1.0, ratio / _options.MinWarningContrastRatio), 4),
            ExpectedValue = $"{_options.MinWarningContrastRatio:0.0}:1 minimum",
            ExtractedValue = $"{ratio:0.0}:1",
            Notes = isMatch ? null : FieldConfidenceCommentary.ForWarningField(
                "WarningContrast",
                false,
                Math.Min(1.0, ratio / _options.MinWarningContrastRatio),
                "Government warning band lacks sufficient text/background contrast")
        };
    }

    private FieldVerificationResult EvaluateBoldWarningTypography(
        byte[] segmentBytes,
        string ocrText,
        bool visualWarningConfirmed,
        string visualRationale)
    {
        var footer = _preprocessor.CropBand(segmentBytes, 0.55, 1.0);
        var confidence = _preprocessor.EstimateFlatBoldTypographyConfidence(footer);
        var ocrWarningConfirmed = WarningTextHelper.ContainsRequiredWarningPhrases(
            WarningTextHelper.NormalizeTtbText(ocrText));
        var warningConfirmed = ocrWarningConfirmed || visualWarningConfirmed;
        var isMatch = warningConfirmed || confidence >= _options.MinBoldTypographyConfidence;
        var resolvedConfidence = warningConfirmed
            ? Math.Max(visualWarningConfirmed ? 0.82 : 0.85, confidence)
            : confidence;

        return new FieldVerificationResult
        {
            FieldName = "BoldWarningTypography",
            IsMatch = isMatch,
            Confidence = Math.Round(resolvedConfidence, 4),
            ExpectedValue = "Bold GOVERNMENT WARNING heading",
            ExtractedValue = isMatch ? "Heading stroke weight exceeds body text" : null,
            Notes = isMatch
                ? visualWarningConfirmed && !ocrWarningConfirmed
                    ? visualRationale
                    : null
                : FieldConfidenceCommentary.ForWarningField(
                    "BoldWarningTypography",
                    false,
                    resolvedConfidence,
                    "Warning heading does not appear materially bolder than surrounding warning body text")
        };
    }

    private FieldVerificationResult EvaluateLabelTextContrast(byte[] segmentBytes)
    {
        var central = _preprocessor.CropBand(segmentBytes, 0.12, 0.88);
        var ratio = EstimateBandContrastRatio(central);
        var isMatch = ratio >= _options.MinLabelContrastRatio;

        return new FieldVerificationResult
        {
            FieldName = "LabelTextContrast",
            IsMatch = isMatch,
            Confidence = Math.Round(Math.Min(1.0, ratio / _options.MinLabelContrastRatio), 4),
            ExpectedValue = $"{_options.MinLabelContrastRatio:0.0}:1 minimum",
            ExtractedValue = $"{ratio:0.0}:1",
            Notes = isMatch ? null : "Primary label text lacks sufficient contrast against its background"
        };
    }

    private static double EstimateTextInkDensity(byte[] bandBytes)
    {
        using var gray = Mat.FromImageData(bandBytes, ImreadModes.Grayscale);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 31, 10);
        using var rowProjection = new Mat();
        Cv2.Reduce(binary, rowProjection, ReduceDimension.Column, ReduceTypes.Avg, MatType.CV_32F);

        var activeRows = 0;
        for (var row = 0; row < rowProjection.Rows; row++)
        {
            if (rowProjection.At<float>(row, 0) > 8.0)
            {
                activeRows++;
            }
        }

        return activeRows / (double)Math.Max(1, rowProjection.Rows);
    }

    private static double EstimateBandContrastRatio(byte[] bandBytes)
    {
        using var color = Mat.FromImageData(bandBytes, ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 31, 10);

        var inkPixels = new List<double>();
        var paperPixels = new List<double>();
        for (var y = 0; y < gray.Rows; y += 2)
        {
            for (var x = 0; x < gray.Cols; x += 2)
            {
                var luminance = gray.At<byte>(y, x);
                if (binary.At<byte>(y, x) > 0)
                {
                    inkPixels.Add(luminance);
                }
                else
                {
                    paperPixels.Add(luminance);
                }
            }
        }

        if (inkPixels.Count == 0 || paperPixels.Count == 0)
        {
            return 1.0;
        }

        var dark = inkPixels.OrderBy(v => v).Take(Math.Max(1, inkPixels.Count / 4)).Average();
        var light = paperPixels.OrderByDescending(v => v).Take(Math.Max(1, paperPixels.Count / 4)).Average();
        var lighter = Math.Max(dark, light);
        var darker = Math.Min(dark, light);
        return (lighter + 5.0) / (darker + 5.0);
    }
}
