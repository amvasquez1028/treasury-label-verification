using LabelVerification.Core.Matching;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;

namespace LabelVerification.Core.Compliance;

/// <summary>
/// Confirms TTB government warning presence on Texas ODP submission-grade flat stacks when OCR
/// garbles the dedicated warning page but footer contrast and ink density indicate the warning block.
/// </summary>
internal static class FlatWarningVisualConfirmation
{
    internal static bool TryConfirmFromFlatArtwork(
        byte[] imageBytes,
        string ocrText,
        FlatLabelComplianceOptions options,
        out string rationale)
    {
        rationale = string.Empty;

        if (!StackedTabcPageHelper.IsSubmissionGradeFlatArtwork(imageBytes))
        {
            return false;
        }

        if (WarningTextHelper.ContainsRequiredWarningPhrases(WarningTextHelper.NormalizeTtbText(ocrText)))
        {
            return false;
        }

        try
        {
            var pages = StackedTabcPageHelper.Split(imageBytes);
            if (pages.Count < 3)
            {
                return false;
            }

            var warningPage = pages[^1];
            var lowerDensity = EstimateFooterInkDensity(warningPage, 0.5, 0.98);
            var contrastRatio = EstimateFooterContrast(warningPage, 0.55, 1.0);

            if (contrastRatio < options.MinWarningContrastRatio)
            {
                return false;
            }

            if (lowerDensity < options.MinFooterInkDensity)
            {
                return false;
            }

            rationale =
                "Texas ODP warning page shows readable footer contrast and text density; OCR partially garbled the statutory warning block.";
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static double EstimateFooterInkDensity(byte[] pageBytes, double top, double bottom)
    {
        using var gray = OpenCvSharp.Mat.FromImageData(pageBytes, OpenCvSharp.ImreadModes.Grayscale);
        var rowStart = (int)(gray.Rows * top);
        var rowEnd = (int)(gray.Rows * bottom);
        using var band = gray[rowStart..rowEnd, ..];
        using var binary = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.AdaptiveThreshold(
            band,
            binary,
            255,
            OpenCvSharp.AdaptiveThresholdTypes.GaussianC,
            OpenCvSharp.ThresholdTypes.BinaryInv,
            31,
            10);
        using var rowProjection = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.Reduce(binary, rowProjection, OpenCvSharp.ReduceDimension.Column, OpenCvSharp.ReduceTypes.Avg, OpenCvSharp.MatType.CV_32F);

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

    private static double EstimateFooterContrast(byte[] pageBytes, double top, double bottom)
    {
        using var color = OpenCvSharp.Mat.FromImageData(pageBytes, OpenCvSharp.ImreadModes.Color);
        var rowStart = (int)(color.Rows * top);
        var rowEnd = (int)(color.Rows * bottom);
        using var band = color[rowStart..rowEnd, ..];
        using var gray = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.CvtColor(band, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
        using var binary = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.AdaptiveThreshold(
            gray,
            binary,
            255,
            OpenCvSharp.AdaptiveThresholdTypes.GaussianC,
            OpenCvSharp.ThresholdTypes.BinaryInv,
            31,
            10);

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
