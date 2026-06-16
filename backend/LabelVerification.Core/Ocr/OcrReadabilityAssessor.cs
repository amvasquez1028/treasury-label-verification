using System.Text.RegularExpressions;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Ocr;

public sealed record OcrReadabilityAssessment(
    bool IsReadable,
    string? Reason,
    string? AgentGuidance);

public interface IOcrReadabilityAssessor
{
    OcrReadabilityAssessment Assess(byte[] imageBytes, string ocrText);
}

public sealed class OcrReadabilityAssessor : IOcrReadabilityAssessor
{
    private readonly IImagePreprocessor _preprocessor;
    private readonly ReadabilityOptions _options;

    public OcrReadabilityAssessor(IImagePreprocessor preprocessor, IOptions<ReadabilityOptions> options)
    {
        _preprocessor = preprocessor;
        _options = options.Value;
    }

    public OcrReadabilityAssessment Assess(byte[] imageBytes, string ocrText)
    {
        var isFlatArtwork = _preprocessor.ClassifyImage(imageBytes) == LabelImageKind.FlatArtwork;
        var minAlphanumeric = isFlatArtwork
            ? _options.FlatArtworkMinAlphanumericCharacters
            : _options.MinAlphanumericCharacters;
        var minWords = isFlatArtwork
            ? _options.FlatArtworkMinSubstantiveWords
            : _options.MinSubstantiveWords;
        var blurThreshold = isFlatArtwork
            ? _options.FlatArtworkMinBlurVariance
            : _options.MinBlurVariance;

        var alphanumericCount = CountAlphanumeric(ocrText);
        if (alphanumericCount < minAlphanumeric)
        {
            return Unreadable(
                "Too little readable text was detected on this image.",
                "Request a clearer label image from the applicant with the full label in frame.");
        }

        var wordCount = CountSubstantiveWords(ocrText);
        if (wordCount < minWords)
        {
            return Unreadable(
                "OCR could not identify enough label text to verify.",
                "Request a clearer label image from the applicant with even lighting and the full warning visible.");
        }

        var readabilityProbe = isFlatArtwork
            ? _preprocessor.CropTabcFlatArtwork(imageBytes)
            : _preprocessor.TryCropLabelRegion(imageBytes);
        var blurVariance = _preprocessor.EstimateBlurVariance(readabilityProbe);
        if (_preprocessor.ClassifyImage(imageBytes) == LabelImageKind.BottlePhoto)
        {
            blurThreshold = Math.Min(blurThreshold, 18.0);
        }

        if (blurVariance < blurThreshold && wordCount < minWords * 2)
        {
            return Unreadable(
                "The image appears too blurry for reliable text extraction.",
                "Request a sharper label image from the applicant with the label filling most of the frame.");
        }

        var contrastStdDev = _preprocessor.EstimateContrastStdDev(readabilityProbe);
        if (contrastStdDev < _options.MinContrastStdDev)
        {
            return Unreadable(
                "The image has very low contrast or heavy glare.",
                "Request a label image from the applicant with reduced glare and even indoor lighting.");
        }

        return new OcrReadabilityAssessment(true, null, null);
    }

    private static int CountAlphanumeric(string text)
    {
        return text.Count(char.IsLetterOrDigit);
    }

    private static int CountSubstantiveWords(string text)
    {
        return Regex.Matches(text, @"[A-Za-z]{2,}").Count;
    }

    private static OcrReadabilityAssessment Unreadable(string reason, string guidance) =>
        new(false, reason, guidance);
}
