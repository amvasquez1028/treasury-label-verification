using OpenCvSharp;

namespace LabelVerification.Core.Ocr;

/// <summary>
/// OpenCV ink-projection field band detector for submission-grade flat labels.
/// Locates text-dense horizontal regions so OCR runs on ROIs instead of full pages.
/// </summary>
internal static class FlatArtworkFieldBandDetector
{
    internal readonly record struct TextBand(double TopRatio, double BottomRatio, FlatArtworkFieldKind Kind);

    internal enum FlatArtworkFieldKind
    {
        Header,
        Contents,
        Footer,
        Detected,
    }

    internal static IReadOnlyList<TextBand> Detect(byte[] imageBytes)
    {
        using var gray = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(
            gray,
            binary,
            255,
            AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.BinaryInv,
            31,
            10);

        var rowDensity = new float[binary.Rows];
        for (var row = 0; row < binary.Rows; row += 1)
        {
            using var rowMat = binary.Row(row);
            rowDensity[row] = Cv2.CountNonZero(rowMat) / (float)Math.Max(1, binary.Cols);
        }

        var detected = ExtractInkBands(rowDensity, minDensity: 0.012, minBandHeightRatio: 0.06, mergeGapRatio: 0.025)
            .Select(b => b with { Kind = FlatArtworkFieldKind.Detected })
            .ToList();

        var mandatory = new[]
        {
            new TextBand(0.0, 0.28, FlatArtworkFieldKind.Header),
            new TextBand(0.32, 0.72, FlatArtworkFieldKind.Contents),
            new TextBand(0.58, 1.0, FlatArtworkFieldKind.Footer),
        };

        return MergeBands(mandatory.Concat(detected)).OrderBy(b => b.TopRatio).ToArray();
    }

    private static IEnumerable<TextBand> ExtractInkBands(
        float[] rowDensity,
        double minDensity,
        double minBandHeightRatio,
        double mergeGapRatio)
    {
        var height = rowDensity.Length;
        var minBandRows = (int)(height * minBandHeightRatio);
        var mergeGapRows = (int)(height * mergeGapRatio);
        var bands = new List<(int Top, int Bottom)>();
        var start = -1;

        for (var row = 0; row < height; row += 1)
        {
            var active = rowDensity[row] >= minDensity;
            if (active && start < 0)
            {
                start = row;
            }
            else if (!active && start >= 0)
            {
                if (row - start >= minBandRows)
                {
                    bands.Add((start, row));
                }

                start = -1;
            }
        }

        if (start >= 0 && height - start >= minBandRows)
        {
            bands.Add((start, height));
        }

        if (bands.Count == 0)
        {
            yield break;
        }

        var merged = new List<(int Top, int Bottom)> { bands[0] };
        for (var i = 1; i < bands.Count; i += 1)
        {
            var previous = merged[^1];
            var current = bands[i];
            if (current.Top - previous.Bottom <= mergeGapRows)
            {
                merged[^1] = (previous.Top, current.Bottom);
            }
            else
            {
                merged.Add(current);
            }
        }

        foreach (var (top, bottom) in merged)
        {
            var pad = (int)(height * 0.015);
            var topRatio = Math.Clamp((top - pad) / (double)height, 0.0, 0.98);
            var bottomRatio = Math.Clamp((bottom + pad) / (double)height, topRatio + 0.04, 1.0);
            yield return new TextBand(topRatio, bottomRatio, FlatArtworkFieldKind.Detected);
        }
    }

    private static IReadOnlyList<TextBand> MergeBands(IEnumerable<TextBand> bands)
    {
        var ordered = bands.OrderBy(b => b.TopRatio).ToList();
        if (ordered.Count == 0)
        {
            return ordered;
        }

        var merged = new List<TextBand> { ordered[0] };
        for (var i = 1; i < ordered.Count; i += 1)
        {
            var previous = merged[^1];
            var current = ordered[i];
            if (current.TopRatio <= previous.BottomRatio + 0.04)
            {
                merged[^1] = new TextBand(
                    previous.TopRatio,
                    Math.Max(previous.BottomRatio, current.BottomRatio),
                    PreferKind(previous.Kind, current.Kind));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static FlatArtworkFieldKind PreferKind(
        FlatArtworkFieldKind left,
        FlatArtworkFieldKind right)
    {
        if (left == FlatArtworkFieldKind.Footer || right == FlatArtworkFieldKind.Footer)
        {
            return FlatArtworkFieldKind.Footer;
        }

        if (left == FlatArtworkFieldKind.Contents || right == FlatArtworkFieldKind.Contents)
        {
            return FlatArtworkFieldKind.Contents;
        }

        if (left == FlatArtworkFieldKind.Header || right == FlatArtworkFieldKind.Header)
        {
            return FlatArtworkFieldKind.Header;
        }

        return FlatArtworkFieldKind.Detected;
    }
}
