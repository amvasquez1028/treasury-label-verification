using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LabelVerification.Core.Compliance;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Tesseract;

namespace LabelVerification.Core.Ocr;

public interface ITesseractEngineProvider
{
    TesseractEngine Engine { get; }
    EngineLease RentEngine(CancellationToken cancellationToken = default);
    void WarmUp();
    bool IsReady { get; }
}

public readonly struct EngineLease : IDisposable
{
    private readonly SemaphoreSlim? _gate;

    public TesseractEngine Engine { get; }

    internal EngineLease(TesseractEngine engine, SemaphoreSlim? gate)
    {
        Engine = engine;
        _gate = gate;
    }

    public void Dispose()
    {
        _gate?.Release();
    }
}

public sealed class TesseractEngineProvider : ITesseractEngineProvider, IDisposable
{
    private readonly TesseractEngine[] _engines;
    private readonly SemaphoreSlim[] _poolGates;
    private bool _warmed;

    public TesseractEngineProvider(IOptions<OcrOptions> options)
    {
        var ocrOptions = options.Value;
        var path = ResolveTessDataPath(ocrOptions.TessDataPath);
        var poolSize = Math.Clamp(ocrOptions.FlatArtworkEnginePoolSize, 1, 6);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            poolSize = 1;
        }
        _engines = Enumerable.Range(0, poolSize)
            .Select(_ => new TesseractEngine(path, "eng", EngineMode.Default))
            .ToArray();
        _poolGates = _engines.Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    }

    public TesseractEngine Engine => _engines[0];

    public EngineLease RentEngine(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < _poolGates.Length; i++)
            {
                if (_poolGates[i].Wait(0, cancellationToken))
                {
                    return new EngineLease(_engines[i], _poolGates[i]);
                }
            }

            Thread.Sleep(5);
        }
    }

    public bool IsReady => _warmed;

    public void WarmUp()
    {
        if (_warmed)
        {
            return;
        }

        using var pix = Pix.LoadFromMemory(CreateWarmupPng());
        foreach (var engine in _engines)
        {
            using var page = engine.Process(pix);
            _ = page.GetText();
        }

        _warmed = true;
    }

    private static byte[] CreateWarmupPng()
    {
        // Minimal 1x1 PNG so WarmUp does not depend on OpenCV native libraries at startup.
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    }

    public void Dispose()
    {
        foreach (var engine in _engines)
        {
            engine.Dispose();
        }

        foreach (var gate in _poolGates)
        {
            gate.Dispose();
        }
    }

    private static string ResolveTessDataPath(string configuredPath)
    {
        var candidates = new[]
        {
            configuredPath,
            Path.Combine(AppContext.BaseDirectory, configuredPath),
            Path.Combine(Directory.GetCurrentDirectory(), configuredPath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", configuredPath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "treasury-label-verification", configuredPath))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "eng.traineddata")))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new DirectoryNotFoundException($"Tessdata directory not found. Checked: {string.Join(", ", candidates)}");
    }
}

public enum LabelImageKind
{
    FlatArtwork,
    BottlePhoto,
    Screenshot
}

public interface IImagePreprocessor
{
    byte[] UpscaleIfSmall(byte[] imageBytes, int minSide = 800);
    LabelImageKind ClassifyImage(byte[] imageBytes);
    byte[] Preprocess(byte[] imageBytes);
    byte[] PreprocessForOcr(byte[] imageBytes);
    byte[] PreprocessGrayscaleRaw(byte[] imageBytes);
    byte[] CropBand(byte[] imageBytes, double topRatio, double bottomRatio);
    byte[] TryCropLabelRegion(byte[] imageBytes);
    byte[] CropTabcFlatArtwork(byte[] imageBytes);
    bool IsDarkLabelArtwork(byte[] imageBytes);
    double EstimateBoldPhraseConfidence(byte[] imageBytes, string phrase);
    double EstimateBlurVariance(byte[] imageBytes);
    byte[] PreprocessBlueGlassLabel(byte[] imageBytes);
    byte[] PreprocessWhiteOnDarkLabel(byte[] imageBytes);
    byte[] PreprocessFlatLabel(byte[] imageBytes);
    byte[] PreprocessLineArtFlatLabel(byte[] imageBytes);
    byte[] CropCentralLabelBand(byte[] imageBytes);
    byte[] CropBottomLabelSticker(byte[] imageBytes);
    double EstimateContrastStdDev(byte[] imageBytes);
    double EstimateFlatBoldTypographyConfidence(byte[] imageBytes);
}

public sealed class OpenCvImagePreprocessor : IImagePreprocessor
{
    public byte[] UpscaleIfSmall(byte[] imageBytes, int minSide = 800)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var shortest = Math.Min(mat.Width, mat.Height);
        var targetMin = ClassifyImage(imageBytes) == LabelImageKind.FlatArtwork ? minSide : Math.Max(minSide, 1200);
        if (shortest >= targetMin)
        {
            return imageBytes;
        }

        var scale = targetMin / (double)shortest;
        using var resized = new Mat();
        Cv2.Resize(mat, resized, new Size(), scale, scale, InterpolationFlags.Cubic);
        return resized.ToBytes(".png");
    }

    public LabelImageKind ClassifyImage(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var channels = mat.Split();
        using var hsv = new Mat();
        Cv2.CvtColor(mat, hsv, ColorConversionCodes.BGR2HSV);
        var hsvChannels = hsv.Split();
        Cv2.MeanStdDev(hsvChannels[1], out _, out Scalar satStd);
        Cv2.MeanStdDev(mat, out Scalar colorMean, out Scalar colorStd);
        foreach (var channel in channels)
        {
            channel.Dispose();
        }
        foreach (var channel in hsvChannels)
        {
            channel.Dispose();
        }

        var aspect = mat.Width / (double)Math.Max(1, mat.Height);
        if (aspect > 1.35 && mat.Width >= 900)
        {
            return LabelImageKind.Screenshot;
        }

        if (HasBrightLabelBackground(mat))
        {
            return LabelImageKind.FlatArtwork;
        }

        if (colorMean.Val0 >= 155 && colorMean.Val1 >= 155 && colorMean.Val2 >= 155 && colorStd.Val0 < 65)
        {
            return LabelImageKind.FlatArtwork;
        }

        var tallAspect = mat.Height / (double)Math.Max(1, mat.Width);
        if (mat.Width >= 2000 && mat.Height >= 2500 && tallAspect > 1.15)
        {
            return LabelImageKind.FlatArtwork;
        }

        var isPhoto = satStd.Val0 > 16 && colorStd.Val0 > 24 && tallAspect > 1.02;
        return isPhoto ? LabelImageKind.BottlePhoto : LabelImageKind.FlatArtwork;
    }

    private static bool HasBrightLabelBackground(Mat color)
    {
        var points = new[]
        {
            new Point(12, 12),
            new Point(color.Width - 12, 12),
            new Point(12, color.Height - 12),
            new Point(color.Width - 12, color.Height - 12),
        };

        var samples = points
            .Where(p => p.X >= 0 && p.Y >= 0 && p.X < color.Width && p.Y < color.Height)
            .Select(p => color.At<Vec3b>(p.Y, p.X))
            .Select(pixel => (pixel.Item0 + pixel.Item1 + pixel.Item2) / 3.0)
            .ToArray();

        return samples.Length > 0 && samples.Average() >= 205;
    }

    public byte[] PreprocessForOcr(byte[] imageBytes)
    {
        var scaled = UpscaleIfSmall(imageBytes);
        return ClassifyImage(scaled) switch
        {
            LabelImageKind.BottlePhoto => PreprocessPhoto(scaled),
            LabelImageKind.Screenshot => PreprocessScreenshot(scaled),
            _ => PreprocessFlat(scaled)
        };
    }

    public byte[] Preprocess(byte[] imageBytes) => PreprocessForOcr(imageBytes);

    public byte[] PreprocessGrayscaleRaw(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var clahe = Cv2.CreateCLAHE(2.5, new Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(mat, enhanced);
        using var sharpened = new Mat();
        using var kernel = Mat.FromArray(new float[,]
        {
            { 0, -1, 0 },
            { -1, 5, -1 },
            { 0, -1, 0 },
        });
        Cv2.Filter2D(enhanced, sharpened, MatType.CV_8U, kernel);
        return sharpened.ToBytes(".png");
    }

    private static byte[] PreprocessFlat(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var denoised = new Mat();
        Cv2.GaussianBlur(mat, denoised, new Size(3, 3), 0);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(denoised, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 10);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        using var morphed = new Mat();
        Cv2.MorphologyEx(binary, morphed, MorphTypes.Close, kernel);
        return morphed.ToBytes(".png");
    }

    private static byte[] PreprocessPhoto(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var clahe = Cv2.CreateCLAHE(3.0, new Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(mat, enhanced);
        using var denoised = new Mat();
        Cv2.BilateralFilter(enhanced, denoised, 9, 75, 75);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(denoised, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 35, 11);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        using var morphed = new Mat();
        Cv2.MorphologyEx(binary, morphed, MorphTypes.Close, kernel);
        return morphed.ToBytes(".png");
    }

    private static byte[] PreprocessScreenshot(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var clahe = Cv2.CreateCLAHE(2.0, new Size(12, 12));
        using var enhanced = new Mat();
        clahe.Apply(mat, enhanced);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(enhanced, binary, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 41, 12);
        return binary.ToBytes(".png");
    }

    public byte[] CropBand(byte[] imageBytes, double topRatio, double bottomRatio)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        var top = (int)(mat.Height * topRatio);
        var height = (int)(mat.Height * (bottomRatio - topRatio));
        height = Math.Max(1, Math.Min(height, mat.Height - top));
        using var roi = new Mat(mat, new OpenCvSharp.Rect(0, top, mat.Width, height));
        return roi.ToBytes(".png");
    }

    public byte[] TryCropLabelRegion(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 150);
        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var imageArea = color.Width * color.Height;
        OpenCvSharp.Rect? best = null;
        var bestScore = 0.0;

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            var area = rect.Width * rect.Height;
            if (area < imageArea * 0.08 || area > imageArea * 0.92)
            {
                continue;
            }

            var aspect = rect.Height / (double)Math.Max(1, rect.Width);
            if (aspect < 0.5 || aspect > 4.5)
            {
                continue;
            }

            var centerX = rect.X + rect.Width / 2.0;
            var centerY = rect.Y + rect.Height / 2.0;
            var distFromCenter = Math.Abs(centerX - color.Width / 2.0) / color.Width
                + Math.Abs(centerY - color.Height / 2.0) / color.Height;
            var score = area / imageArea - distFromCenter * 0.3;
            if (score > bestScore)
            {
                bestScore = score;
                best = rect;
            }
        }

        if (best is null)
        {
            return imageBytes;
        }

        var pad = (int)(Math.Min(best.Value.Width, best.Value.Height) * 0.04);
        var x = Math.Max(0, best.Value.X - pad);
        var y = Math.Max(0, best.Value.Y - pad);
        var w = Math.Min(color.Width - x, best.Value.Width + pad * 2);
        var h = Math.Min(color.Height - y, best.Value.Height + pad * 2);
        using var roi = new Mat(color, new OpenCvSharp.Rect(x, y, w, h));
        return roi.ToBytes(".png");
    }

    public byte[] CropTabcFlatArtwork(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        if (!IsTabcPortraitScan(color))
        {
            return imageBytes;
        }

        var top = (int)(color.Height * 0.11);
        var height = (int)(color.Height * 0.84);
        height = Math.Max(1, Math.Min(height, color.Height - top));
        using var trimmed = new Mat(color, new OpenCvSharp.Rect(0, top, color.Width, height));
        return TrimNonBackgroundMargins(trimmed);
    }

    public bool IsDarkLabelArtwork(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(gray, out Scalar mean, out _);
        return mean.Val0 < 145;
    }

    private static bool IsTabcPortraitScan(Mat color)
    {
        var tallAspect = color.Height / (double)Math.Max(1, color.Width);
        return color.Width >= 1800 && color.Height >= 2200 && tallAspect > 1.1;
    }

    private static byte[] TrimNonBackgroundMargins(Mat color)
    {
        using var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, 235, 255, ThresholdTypes.BinaryInv);

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return color.ToBytes(".png");
        }

        var bounds = contours
            .Select(Cv2.BoundingRect)
            .Aggregate((a, b) => new OpenCvSharp.Rect(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Max(a.X + a.Width, b.X + b.Width) - Math.Min(a.X, b.X),
                Math.Max(a.Y + a.Height, b.Y + b.Height) - Math.Min(a.Y, b.Y)));

        var pad = (int)(Math.Min(bounds.Width, bounds.Height) * 0.02);
        var x = Math.Max(0, bounds.X - pad);
        var y = Math.Max(0, bounds.Y - pad);
        var w = Math.Min(color.Width - x, bounds.Width + pad * 2);
        var h = Math.Min(color.Height - y, bounds.Height + pad * 2);
        using var roi = new Mat(color, new OpenCvSharp.Rect(x, y, w, h));
        return roi.ToBytes(".png");
    }

    public double EstimateBoldPhraseConfidence(byte[] imageBytes, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return 0;
        }

        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(mat, binary, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.BinaryInv, 25, 15);

        using var horizontal = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 1));
        using var boldRegions = new Mat();
        Cv2.MorphologyEx(binary, boldRegions, MorphTypes.Close, horizontal);

        var inkRatio = Cv2.CountNonZero(boldRegions) / (double)(boldRegions.Rows * boldRegions.Cols);
        var phraseWeight = Math.Min(1.0, phrase.Length / 20.0);
        return Math.Round(Math.Min(1.0, inkRatio * 8 * phraseWeight), 4);
    }

    public double EstimateBlurVariance(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var laplacian = new Mat();
        Cv2.Laplacian(mat, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out Scalar stddev);
        return stddev.Val0 * stddev.Val0;
    }

    public double EstimateContrastStdDev(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        Cv2.MeanStdDev(mat, out _, out Scalar stddev);
        return stddev.Val0;
    }

    public byte[] PreprocessBlueGlassLabel(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        using var hsv = new Mat();
        Cv2.CvtColor(color, hsv, ColorConversionCodes.BGR2HSV);
        var channels = hsv.Split();
        using var saturation = channels[1];
        using var value = channels[2];
        foreach (var channel in channels)
        {
            if (!ReferenceEquals(channel, saturation) && !ReferenceEquals(channel, value))
            {
                channel.Dispose();
            }
        }

        using var bright = new Mat();
        Cv2.Threshold(value, bright, 150, 255, ThresholdTypes.Binary);
        using var lowSat = new Mat();
        Cv2.Threshold(saturation, lowSat, 80, 255, ThresholdTypes.BinaryInv);
        using var mask = new Mat();
        Cv2.BitwiseAnd(bright, lowSat, mask);
        using var clahe = Cv2.CreateCLAHE(3.0, new Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(mask, enhanced);
        using var inverted = new Mat();
        Cv2.BitwiseNot(enhanced, inverted);
        return inverted.ToBytes(".png");
    }

    public byte[] PreprocessWhiteOnDarkLabel(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        using var lab = new Mat();
        Cv2.CvtColor(color, lab, ColorConversionCodes.BGR2Lab);
        var channels = lab.Split();
        using var lightness = channels[0];
        foreach (var channel in channels)
        {
            if (!ReferenceEquals(channel, lightness))
            {
                channel.Dispose();
            }
        }

        using var clahe = Cv2.CreateCLAHE(2.5, new Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(lightness, enhanced);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(enhanced, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 8);
        return binary.ToBytes(".png");
    }

    public byte[] PreprocessFlatLabel(byte[] imageBytes) => PreprocessFlat(imageBytes);

    public byte[] PreprocessLineArtFlatLabel(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var denoised = new Mat();
        Cv2.GaussianBlur(mat, denoised, new Size(3, 3), 0);
        using var binary = new Mat();
        Cv2.Threshold(denoised, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        using var morphed = new Mat();
        Cv2.MorphologyEx(binary, morphed, MorphTypes.Close, kernel);
        return morphed.ToBytes(".png");
    }

    public double EstimateFlatBoldTypographyConfidence(byte[] imageBytes)
    {
        using var gray = Mat.FromImageData(imageBytes, ImreadModes.Grayscale);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 31, 8);

        var headingHeight = MedianTextStrokeHeight(binary, 0.0, 0.28);
        var bodyHeight = MedianTextStrokeHeight(binary, 0.28, 1.0);
        if (headingHeight <= 0 || bodyHeight <= 0)
        {
            return EstimateBoldPhraseConfidence(imageBytes, "GOVERNMENT WARNING:");
        }

        var ratio = headingHeight / bodyHeight;
        var densityHeading = InkDensity(binary, 0.0, 0.28);
        var densityBody = InkDensity(binary, 0.28, 1.0);
        var densityBoost = densityHeading > densityBody * 1.05 ? 0.15 : 0.0;
        return Math.Round(Math.Min(1.0, Math.Max(0, (ratio - 0.95) * 1.4 + densityBoost)), 4);
    }

    private static double MedianTextStrokeHeight(Mat binary, double topRatio, double bottomRatio)
    {
        var top = (int)(binary.Rows * topRatio);
        var height = (int)(binary.Rows * (bottomRatio - topRatio));
        height = Math.Max(1, Math.Min(height, binary.Rows - top));
        using var band = new Mat(binary, new OpenCvSharp.Rect(0, top, binary.Cols, height));

        Cv2.FindContours(band, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var heights = contours
            .Select(Cv2.BoundingRect)
            .Where(rect => rect.Width > rect.Height * 1.5 && rect.Width >= 8)
            .Select(rect => rect.Height)
            .OrderBy(value => value)
            .ToArray();

        if (heights.Length == 0)
        {
            return 0;
        }

        return heights[heights.Length / 2];
    }

    private static double InkDensity(Mat binary, double topRatio, double bottomRatio)
    {
        var top = (int)(binary.Rows * topRatio);
        var height = (int)(binary.Rows * (bottomRatio - topRatio));
        height = Math.Max(1, Math.Min(height, binary.Rows - top));
        using var band = new Mat(binary, new OpenCvSharp.Rect(0, top, binary.Cols, height));
        return Cv2.CountNonZero(band) / (double)(band.Rows * band.Cols);
    }

    public byte[] CropCentralLabelBand(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var left = (int)(color.Width * 0.18);
        var top = (int)(color.Height * 0.22);
        var width = (int)(color.Width * 0.64);
        var height = (int)(color.Height * 0.58);
        width = Math.Max(1, Math.Min(width, color.Width - left));
        height = Math.Max(1, Math.Min(height, color.Height - top));
        using var roi = new Mat(color, new OpenCvSharp.Rect(left, top, width, height));
        return roi.ToBytes(".png");
    }

    public byte[] CropBottomLabelSticker(byte[] imageBytes)
    {
        using var color = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var left = (int)(color.Width * 0.22);
        var top = (int)(color.Height * 0.68);
        var width = (int)(color.Width * 0.56);
        var height = (int)(color.Height * 0.24);
        width = Math.Max(1, Math.Min(width, color.Width - left));
        height = Math.Max(1, Math.Min(height, color.Height - top));
        using var roi = new Mat(color, new OpenCvSharp.Rect(left, top, width, height));
        return roi.ToBytes(".png");
    }
}

public interface IOcrService
{
    Task<string> ExtractTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken,
        LabelImageKind? kindHint = null);

    /// <summary>
    /// Targeted cert-page and warning-footer OCR merged after the main pass when submission-grade fields are still missing.
    /// </summary>
    Task<string> ExtractSubmissionGradeSupplementAsync(
        byte[] imageBytes,
        string existingCorpus,
        CancellationToken cancellationToken);

    Task<double> GetBoldConfidenceAsync(byte[] imageBytes, string phrase, CancellationToken cancellationToken);
}

public sealed class OcrService : IOcrService
{
    private readonly ITesseractEngineProvider _engineProvider;
    private readonly IImagePreprocessor _preprocessor;
    private readonly int _flatArtworkMaxOcrSide;
    private readonly bool _useFieldBandTargetedOcr;
    private readonly int _submissionGradeTargetMs;
    private readonly int _perLabelWallClockMs;
    private readonly int _submissionGradeSupplementWallClockMs;

    public OcrService(
        ITesseractEngineProvider engineProvider,
        IImagePreprocessor preprocessor,
        IOptions<OcrOptions> ocrOptions)
    {
        _engineProvider = engineProvider;
        _preprocessor = preprocessor;
        _flatArtworkMaxOcrSide = Math.Clamp(ocrOptions.Value.FlatArtworkMaxOcrSide, 1000, 3200);
        _useFieldBandTargetedOcr = ocrOptions.Value.UseFieldBandTargetedOcr;
        _submissionGradeTargetMs = Math.Clamp(ocrOptions.Value.SubmissionGradeTargetMs, 1500, 15000);
        _perLabelWallClockMs = Math.Clamp(ocrOptions.Value.PerLabelWallClockMs, 2000, 15000);
        _submissionGradeSupplementWallClockMs = Math.Clamp(
            ocrOptions.Value.SubmissionGradeSupplementWallClockMs,
            1000,
            12000);
    }

    public Task<string> ExtractTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken,
        LabelImageKind? kindHint = null)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scaled = _preprocessor.UpscaleIfSmall(imageBytes);
            var kind = kindHint ?? _preprocessor.ClassifyImage(scaled);
            var stackedFlatArtwork = IsStackedFlatArtwork(scaled, kind);
            var submissionGradeStack = stackedFlatArtwork
                && StackedTabcPageHelper.IsSubmissionGradeFlatArtwork(scaled);
            if (submissionGradeStack)
            {
                scaled = DownscaleForOcr(scaled, _flatArtworkMaxOcrSide);
            }
            var prepared = kind == LabelImageKind.FlatArtwork && !stackedFlatArtwork
                ? _preprocessor.CropTabcFlatArtwork(scaled)
                : stackedFlatArtwork
                    ? _preprocessor.CropTabcFlatArtwork(scaled)
                    : scaled;
            var labelCrop = kind is LabelImageKind.BottlePhoto or LabelImageKind.Screenshot
                || (kind == LabelImageKind.FlatArtwork && !stackedFlatArtwork)
                ? _preprocessor.TryCropLabelRegion(prepared)
                : prepared;
            var centralBand = kind == LabelImageKind.BottlePhoto
                ? _preprocessor.CropCentralLabelBand(scaled)
                : labelCrop;
            var bottomSticker = kind == LabelImageKind.BottlePhoto
                ? _preprocessor.CropBottomLabelSticker(scaled)
                : labelCrop;
            var passes = new List<string>();
            var wallTimer = System.Diagnostics.Stopwatch.StartNew();

            if (submissionGradeStack || stackedFlatArtwork)
            {
                passes.AddRange(RunSubmissionGradeFlatPasses(
                    prepared,
                    imageBytes,
                    wallTimer));
            }
            else
            {
                labelCrop = DownscaleForOcr(labelCrop, kind == LabelImageKind.FlatArtwork ? _flatArtworkMaxOcrSide : 1600);
                centralBand = DownscaleForOcr(centralBand, kind == LabelImageKind.FlatArtwork ? _flatArtworkMaxOcrSide : 1600);
                bottomSticker = DownscaleForOcr(bottomSticker, kind == LabelImageKind.FlatArtwork ? _flatArtworkMaxOcrSide : 1600);

                if (kind == LabelImageKind.BottlePhoto)
                {
                    AddBudgetedBottlePhotoPasses(passes, centralBand, bottomSticker, labelCrop, wallTimer, kind);
                }
                else if (kind == LabelImageKind.FlatArtwork)
                {
                    TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessLineArtFlatLabel(labelCrop), PageSegMode.SingleBlock);
                    TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessFlatLabel(labelCrop), PageSegMode.SingleBlock);
                    TryAddPass(
                        passes,
                        wallTimer,
                        kind,
                        _preprocessor.PreprocessFlatLabel(_preprocessor.CropBand(labelCrop, 0.62, 1.0)),
                        PageSegMode.SingleBlock);
                }
                else
                {
                    TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessForOcr(labelCrop), PageSegMode.SingleBlock);
                    TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessGrayscaleRaw(labelCrop), PageSegMode.SingleBlock);
                    TryAddPass(
                        passes,
                        wallTimer,
                        kind,
                        _preprocessor.CropBand(labelCrop, 0.55, 1.0),
                        PageSegMode.SingleBlock);
                    if (!IsBudgetComplete(passes, wallTimer, kind) && kind == LabelImageKind.Screenshot)
                    {
                        TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessForOcr(labelCrop), PageSegMode.SparseText);
                    }
                }
            }

            return MergeOcrCorpus(passes);
        }, cancellationToken);
    }

    private List<string> RunSubmissionGradeFlatPasses(
        byte[] prepared,
        byte[] fullStack,
        System.Diagnostics.Stopwatch wallTimer)
    {
        var passes = new List<string>();
        // Split the full-resolution stack so tall ODP PDFs stay cert + label + warning (3 pages).
        // Splitting after FlatArtworkMaxOcrSide downscale collapses many samples to 2 pages and misses brand ROIs.
        var pages = StackedTabcPageHelper.Split(fullStack);
        var compositeCrop = DownscaleForOcr(prepared, _flatArtworkMaxOcrSide);
        var compositeBottom = _preprocessor.CropBand(compositeCrop, 0.5, 1.0);
        var (brandPage, warningPage) = ResolveSubmissionGradeLabelPages(pages, compositeCrop);
        var warningFooter = _preprocessor.CropBand(warningPage, 0.55, 1.0);
        var certificateBand = _preprocessor.UpscaleIfSmall(
            ExtractCertificateMetadataBand(fullStack),
            minSide: 1600);
        TryAddSubmissionGradePass(
            passes,
            wallTimer,
            0,
            _preprocessor.PreprocessFlatLabel(certificateBand),
            PageSegMode.SingleBlock);
        TryAddSubmissionGradePass(
            passes,
            wallTimer,
            0,
            _preprocessor.PreprocessGrayscaleRaw(certificateBand),
            PageSegMode.SingleBlock);
        TryAddSubmissionGradePass(
            passes,
            wallTimer,
            0,
            _preprocessor.PreprocessFlatLabel(certificateBand),
            PageSegMode.SparseText);
        var parallelCrops = new List<byte[]>
        {
            _preprocessor.PreprocessFlatLabel(brandPage),
            _preprocessor.PreprocessFlatLabel(warningFooter),
            _preprocessor.PreprocessWhiteOnDarkLabel(warningFooter),
            _preprocessor.PreprocessFlatLabel(compositeBottom),
            _preprocessor.PreprocessWhiteOnDarkLabel(compositeBottom),
        };

        if (HasWallBudget(wallTimer))
        {
            TryAddParallelPasses(passes, wallTimer, parallelCrops, PageSegMode.SingleBlock);
        }

        if (!FlatArtworkOcrSufficiency.HasWarningCorpus(MergeOcrCorpus(passes)) && HasWallBudget(wallTimer))
        {
            TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessFlatLabel(warningFooter), PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessWhiteOnDarkLabel(warningFooter), PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessFlatLabel(warningFooter), PageSegMode.SparseText);
        }

        var mergedCorpus = MergeOcrCorpus(passes);
        if (HasWallBudget(wallTimer)
            && pages.Count >= 3
            && FlatArtworkOcrSufficiency.NeedsCertificateMetadata(mergedCorpus))
        {
            TryAddParallelPasses(
                passes,
                wallTimer,
                BuildCertificatePageBandCrops(pages),
                PageSegMode.SingleBlock);
            mergedCorpus = MergeOcrCorpus(passes);
        }

        var includeCertificatePage = pages.Count >= 3
            && FlatArtworkOcrSufficiency.NeedsCertificateMetadata(mergedCorpus);
        if (pages.Count >= 2 || !FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(mergedCorpus))
        {
            foreach (var pageBytes in StackedTabcPageHelper.SelectDeepOcrPages(pages, includeCertificatePage))
            {
                if (!HasWallBudget(wallTimer))
                {
                    break;
                }

                var isCertificatePage = includeCertificatePage
                    && StackedTabcPageHelper.TryGetCertificatePage(pages, out var certPage)
                    && ReferenceEquals(pageBytes, certPage);
                if (isCertificatePage)
                {
                    AddCertificatePagePasses(passes, wallTimer, pageBytes);
                    continue;
                }

                AddFlatArtworkPasses(passes, wallTimer, pageBytes, perPage: true);
            }
        }

        if (!FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(MergeOcrCorpus(passes)))
        {
            TryAddSubmissionGradePass(passes, wallTimer, 0, compositeBottom, PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessFlatLabel(compositeBottom),
                PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessWhiteOnDarkLabel(compositeBottom),
                PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessFlatLabel(_preprocessor.CropBand(compositeCrop, 0.52, 0.72)),
                PageSegMode.SingleBlock);
        }

        return passes;
    }

    private void AddFlatArtworkPasses(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        byte[] pageBytes,
        bool perPage)
    {
        var labelCrop = DownscaleForOcr(
            perPage ? _preprocessor.CropTabcFlatArtwork(pageBytes) : pageBytes,
            _flatArtworkMaxOcrSide);

        if (_useFieldBandTargetedOcr && perPage)
        {
            var bands = FlatArtworkFieldBandDetector.Detect(labelCrop);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessFlatLabel(CropBandForField(labelCrop, bands, FlatArtworkFieldBandDetector.FlatArtworkFieldKind.Header, 0.0, 0.28)),
                PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessFlatLabel(CropBandForField(labelCrop, bands, FlatArtworkFieldBandDetector.FlatArtworkFieldKind.Contents, 0.35, 0.72)),
                PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(
                passes,
                wallTimer,
                0,
                _preprocessor.PreprocessFlatLabel(CropBandForField(labelCrop, bands, FlatArtworkFieldBandDetector.FlatArtworkFieldKind.Footer, 0.58, 1.0)),
                PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessLineArtFlatLabel(labelCrop), PageSegMode.SingleBlock);
            TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessGrayscaleRaw(labelCrop), PageSegMode.SingleBlock);
            return;
        }

        TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessLineArtFlatLabel(labelCrop), PageSegMode.SingleBlock);
        TryAddSubmissionGradePass(passes, wallTimer, 0, _preprocessor.PreprocessFlatLabel(labelCrop), PageSegMode.SingleBlock);
        TryAddSubmissionGradePass(
            passes,
            wallTimer,
            0,
            _preprocessor.PreprocessFlatLabel(_preprocessor.CropBand(labelCrop, 0.62, 1.0)),
            PageSegMode.SingleBlock);
    }

    private (byte[] BrandPage, byte[] WarningPage) ResolveSubmissionGradeLabelPages(
        IReadOnlyList<byte[]> pages,
        byte[] compositeCrop)
    {
        if (pages.Count >= 3)
        {
            return (
                DownscaleForOcr(_preprocessor.CropTabcFlatArtwork(pages[^2]), _flatArtworkMaxOcrSide),
                DownscaleForOcr(_preprocessor.CropTabcFlatArtwork(pages[^1]), _flatArtworkMaxOcrSide));
        }

        if (pages.Count == 2)
        {
            var labelPage = DownscaleForOcr(_preprocessor.CropTabcFlatArtwork(pages[1]), _flatArtworkMaxOcrSide);
            return (labelPage, labelPage);
        }

        if (pages.Count == 1)
        {
            var single = DownscaleForOcr(_preprocessor.CropTabcFlatArtwork(pages[0]), _flatArtworkMaxOcrSide);
            return (single, single);
        }

        return (compositeCrop, compositeCrop);
    }

    private void AddCertificatePagePasses(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        byte[] pageBytes)
    {
        foreach (var crop in BuildCertificatePageBandCrops([pageBytes]))
        {
            TryAddSubmissionGradePass(passes, wallTimer, 0, crop, PageSegMode.SingleBlock);
            if (!HasWallBudget(wallTimer))
            {
                break;
            }

            TryAddSubmissionGradePass(passes, wallTimer, 0, crop, PageSegMode.SparseText);
            if (!HasWallBudget(wallTimer))
            {
                break;
            }
        }
    }

    private List<byte[]> BuildCertificatePageBandCrops(IReadOnlyList<byte[]> pages)
    {
        var crops = new List<byte[]>();
        if (!StackedTabcPageHelper.TryGetCertificatePage(pages, out var rawCertPage))
        {
            return crops;
        }

        var certPageSource = DownscaleForOcr(
            rawCertPage,
            Math.Min(3200, _flatArtworkMaxOcrSide + 600));
        certPageSource = _preprocessor.UpscaleIfSmall(certPageSource, minSide: 1600);
        var certMetadataTable = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.0, 0.58),
            minSide: 1600);
        certMetadataTable = DownscaleForOcr(certMetadataTable, Math.Min(3200, _flatArtworkMaxOcrSide + 600));
        crops.Add(_preprocessor.PreprocessFlatLabel(certPageSource));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(certPageSource));
        crops.Add(_preprocessor.PreprocessWhiteOnDarkLabel(certPageSource));
        crops.Add(_preprocessor.PreprocessFlatLabel(certMetadataTable));
        crops.Add(_preprocessor.PreprocessGrayscaleRaw(certMetadataTable));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(certMetadataTable));

        var sizeRow = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.08, 0.38),
            minSide: 1800);
        sizeRow = DownscaleForOcr(sizeRow, Math.Min(3200, _flatArtworkMaxOcrSide + 800));
        crops.Add(_preprocessor.PreprocessFlatLabel(sizeRow));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(sizeRow));
        crops.Add(_preprocessor.PreprocessGrayscaleRaw(sizeRow));

        if (!_useFieldBandTargetedOcr)
        {
            return crops;
        }

        var certBands = FlatArtworkFieldBandDetector.Detect(certPageSource);
        crops.Add(
            _preprocessor.PreprocessFlatLabel(
                CropBandForField(
                    certPageSource,
                    certBands,
                    FlatArtworkFieldBandDetector.FlatArtworkFieldKind.Header,
                    0.0,
                    0.32)));
        crops.Add(
            _preprocessor.PreprocessFlatLabel(
                CropBandForField(
                    certPageSource,
                    certBands,
                    FlatArtworkFieldBandDetector.FlatArtworkFieldKind.Contents,
                    0.22,
                    0.82)));
        crops.Add(
            _preprocessor.PreprocessGrayscaleRaw(
                _preprocessor.CropBand(certPageSource, 0.08, 0.55)));
        crops.Add(
            _preprocessor.PreprocessFlatLabel(
                _preprocessor.CropBand(rawCertPage, 0.30, 0.78)));

        return crops;
    }

    /// <summary>
    /// Texas ODP stacked PDFs include TTB certificate metadata (size, ABV, origin) above label artwork.
    /// </summary>
    private byte[] ExtractCertificateMetadataBand(byte[] stackBytes)
    {
        var topRatio = StackedTabcPageHelper.IsSubmissionGradeFlatArtwork(stackBytes) ? 0.42 : 0.22;
        var band = _preprocessor.CropBand(stackBytes, 0.0, topRatio);
        band = _preprocessor.UpscaleIfSmall(band, minSide: 1400);
        return DownscaleForOcr(band, _flatArtworkMaxOcrSide);
    }

    private int ResolveWallClockMs(LabelImageKind kind) => _perLabelWallClockMs;

    private bool HasWallBudget(System.Diagnostics.Stopwatch wallTimer, LabelImageKind kind = LabelImageKind.FlatArtwork) =>
        wallTimer.ElapsedMilliseconds < ResolveWallClockMs(kind);

    private void TryAddParallelPasses(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        IReadOnlyList<byte[]> crops,
        PageSegMode mode)
    {
        if (!HasWallBudget(wallTimer) || crops.Count == 0)
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var crop in crops)
            {
                if (!HasWallBudget(wallTimer))
                {
                    break;
                }

                var text = RunPass(crop, mode);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    passes.Add(text);
                }
            }

            return;
        }

        var remainingMs = _perLabelWallClockMs - wallTimer.ElapsedMilliseconds;
        if (remainingMs <= 0)
        {
            return;
        }

        var deadline = TimeSpan.FromMilliseconds(Math.Max(150, remainingMs));
        var tasks = crops
            .Select(crop => Task.Run(() => RunPass(crop, mode)))
            .ToArray();
        if (!Task.WaitAll(tasks, deadline))
        {
            foreach (var task in tasks)
            {
                if (!task.IsCompleted)
                {
                    continue;
                }

                if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
                {
                    passes.Add(task.Result);
                }
            }

            return;
        }
        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
            {
                passes.Add(task.Result);
            }
        }
    }

    private bool TryAddSubmissionGradePass(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        int labelPassBaseline,
        byte[] processedCrop,
        PageSegMode mode)
    {
        const int minLabelPassesBeforeEarlyExit = 4;

        if (!HasWallBudget(wallTimer))
        {
            return false;
        }

        passes.Add(RunPass(processedCrop, mode));
        if (!HasWallBudget(wallTimer))
        {
            return false;
        }

        var labelPassCount = passes.Count - labelPassBaseline;
        if (labelPassCount < minLabelPassesBeforeEarlyExit)
        {
            return true;
        }

        if (wallTimer.ElapsedMilliseconds >= _submissionGradeTargetMs
            && FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(MergeOcrCorpus(passes)))
        {
            return false;
        }

        return !FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(MergeOcrCorpus(passes));
    }

    private void AddBudgetedBottlePhotoPasses(
        List<string> passes,
        byte[] centralBand,
        byte[] bottomSticker,
        byte[] labelCrop,
        System.Diagnostics.Stopwatch wallTimer,
        LabelImageKind kind)
    {
        TryAddPass(passes, wallTimer, kind, centralBand, PageSegMode.SingleBlock);
        TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessFlatLabel(centralBand), PageSegMode.SingleBlock);
        TryAddPass(passes, wallTimer, kind, bottomSticker, PageSegMode.SingleBlock);
        TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessWhiteOnDarkLabel(centralBand), PageSegMode.SingleBlock);
        if (!IsBudgetComplete(passes, wallTimer, kind))
        {
            TryAddPass(passes, wallTimer, kind, _preprocessor.PreprocessBlueGlassLabel(centralBand), PageSegMode.SingleBlock);
            TryAddPass(passes, wallTimer, kind, _preprocessor.CropBand(labelCrop, 0.55, 0.92), PageSegMode.SingleBlock);
        }
    }

    private bool TryAddPass(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        LabelImageKind kind,
        byte[] processedCrop,
        PageSegMode mode)
    {
        if (!HasWallBudget(wallTimer, kind))
        {
            return false;
        }

        passes.Add(RunPass(processedCrop, mode));
        if (!HasWallBudget(wallTimer, kind))
        {
            return false;
        }

        if (kind != LabelImageKind.FlatArtwork)
        {
            return true;
        }

        return !FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(MergeOcrCorpus(passes));
    }

    private bool IsBudgetComplete(List<string> passes, System.Diagnostics.Stopwatch wallTimer, LabelImageKind kind)
    {
        if (!HasWallBudget(wallTimer, kind))
        {
            return true;
        }

        if (kind != LabelImageKind.FlatArtwork)
        {
            return false;
        }

        return FlatArtworkOcrSufficiency.IsSubmissionCorpusComplete(MergeOcrCorpus(passes));
    }

    private byte[] CropBandForField(
        byte[] labelCrop,
        IReadOnlyList<FlatArtworkFieldBandDetector.TextBand> bands,
        FlatArtworkFieldBandDetector.FlatArtworkFieldKind kind,
        double defaultTop,
        double defaultBottom)
    {
        foreach (var band in bands)
        {
            if (band.Kind == kind)
            {
                return _preprocessor.CropBand(labelCrop, band.TopRatio, band.BottomRatio);
            }
        }

        return _preprocessor.CropBand(labelCrop, defaultTop, defaultBottom);
    }

    private static bool IsStackedFlatArtwork(byte[] imageBytes, LabelImageKind kind)
    {
        if (kind != LabelImageKind.FlatArtwork)
        {
            return false;
        }

        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        return mat.Height / (double)Math.Max(1, mat.Width) > 2.0;
    }

    private static byte[] DownscaleForOcr(byte[] imageBytes, int maxSide)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
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

    public Task<string> ExtractSubmissionGradeSupplementAsync(
        byte[] imageBytes,
        string existingCorpus,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!StackedTabcPageHelper.IsSubmissionGradeFlatArtwork(imageBytes))
            {
                return string.Empty;
            }

            var pages = StackedTabcPageHelper.Split(imageBytes);
            if (pages.Count < 3)
            {
                return string.Empty;
            }

            var passes = new List<string>();
            var wallTimer = System.Diagnostics.Stopwatch.StartNew();

            AddSupplementWarningPasses(passes, wallTimer, pages);
            AddSupplementCertificatePasses(passes, wallTimer, imageBytes, pages, includeAddressBands: true);

            return MergeOcrCorpus(passes);
        }, cancellationToken);
    }

    private bool HasSupplementBudget(System.Diagnostics.Stopwatch wallTimer) =>
        wallTimer.ElapsedMilliseconds < _submissionGradeSupplementWallClockMs;

    private void AddSupplementCertificatePasses(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        byte[] fullStack,
        IReadOnlyList<byte[]> pages,
        bool includeAddressBands = false)
    {
        var crops = BuildSupplementCertificateCrops(fullStack, pages, includeAddressBands);
        if (!HasSupplementBudget(wallTimer) || crops.Count == 0)
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var crop in crops)
            {
                if (!HasSupplementBudget(wallTimer))
                {
                    break;
                }

                var text = RunPass(crop, PageSegMode.SingleBlock);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    passes.Add(text);
                }
            }

            return;
        }

        var remainingMs = _submissionGradeSupplementWallClockMs - wallTimer.ElapsedMilliseconds;
        if (remainingMs <= 0)
        {
            return;
        }

        var deadline = TimeSpan.FromMilliseconds(Math.Max(150, remainingMs));
        var tasks = crops
            .Select(crop => Task.Run(() => RunPass(crop, PageSegMode.SingleBlock)))
            .ToArray();
        if (!Task.WaitAll(tasks, deadline))
        {
            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
                {
                    passes.Add(task.Result);
                }
            }

            return;
        }

        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
            {
                passes.Add(task.Result);
            }
        }
    }

    private List<byte[]> BuildSupplementCertificateCrops(
        byte[] fullStack,
        IReadOnlyList<byte[]> pages,
        bool includeAddressBands)
    {
        var crops = new List<byte[]>();
        var stackCertBand = _preprocessor.UpscaleIfSmall(
            ExtractCertificateMetadataBand(fullStack),
            minSide: 1800);
        crops.Add(_preprocessor.PreprocessFlatLabel(stackCertBand));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(stackCertBand));
        crops.Add(_preprocessor.PreprocessGrayscaleRaw(stackCertBand));

        if (!StackedTabcPageHelper.TryGetCertificatePage(pages, out var rawCertPage))
        {
            return crops;
        }

        var certMetadataTable = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.0, 0.58),
            minSide: 1800);
        certMetadataTable = DownscaleForOcr(certMetadataTable, Math.Min(3200, _flatArtworkMaxOcrSide + 800));
        crops.Add(_preprocessor.PreprocessFlatLabel(certMetadataTable));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(certMetadataTable));
        crops.Add(_preprocessor.PreprocessGrayscaleRaw(certMetadataTable));

        var sizeRow = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.08, 0.38),
            minSide: 1800);
        sizeRow = DownscaleForOcr(sizeRow, Math.Min(3200, _flatArtworkMaxOcrSide + 800));
        crops.Add(_preprocessor.PreprocessFlatLabel(sizeRow));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(sizeRow));

        if (includeAddressBands)
        {
            crops.AddRange(BuildCertificateAddressBandCrops(pages));
        }

        return crops;
    }

    private void AddSupplementWarningPasses(
        List<string> passes,
        System.Diagnostics.Stopwatch wallTimer,
        IReadOnlyList<byte[]> pages)
    {
        var warningPage = DownscaleForOcr(
            _preprocessor.CropTabcFlatArtwork(pages[^1]),
            Math.Min(3200, _flatArtworkMaxOcrSide + 800));
        var warningFooter = _preprocessor.CropBand(warningPage, 0.55, 1.0);
        var warningUpper = _preprocessor.CropBand(warningPage, 0.05, 0.55);
        var warningCrops = new[]
        {
            _preprocessor.UpscaleIfSmall(_preprocessor.PreprocessFlatLabel(warningPage), minSide: 2000),
            _preprocessor.UpscaleIfSmall(_preprocessor.PreprocessFlatLabel(warningFooter), minSide: 1800),
            _preprocessor.UpscaleIfSmall(_preprocessor.PreprocessWhiteOnDarkLabel(warningFooter), minSide: 1800),
            _preprocessor.PreprocessLineArtFlatLabel(warningFooter),
            _preprocessor.PreprocessGrayscaleRaw(warningUpper),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var crop in warningCrops)
            {
                if (!HasSupplementBudget(wallTimer))
                {
                    break;
                }

                var text = RunPass(crop, PageSegMode.SingleBlock);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    passes.Add(text);
                }
            }

            return;
        }

        var remainingMs = _submissionGradeSupplementWallClockMs - wallTimer.ElapsedMilliseconds;
        if (remainingMs <= 0)
        {
            return;
        }

        var deadline = TimeSpan.FromMilliseconds(Math.Max(150, remainingMs));
        var tasks = warningCrops
            .Select(crop => Task.Run(() => RunPass(crop, PageSegMode.SingleBlock)))
            .ToArray();
        if (!Task.WaitAll(tasks, deadline))
        {
            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
                {
                    passes.Add(task.Result);
                }
            }

            return;
        }

        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result))
            {
                passes.Add(task.Result);
            }
        }
    }

    private List<byte[]> BuildCertificateAddressBandCrops(IReadOnlyList<byte[]> pages)
    {
        var crops = new List<byte[]>();
        if (!StackedTabcPageHelper.TryGetCertificatePage(pages, out var rawCertPage))
        {
            return crops;
        }

        var certLower = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.32, 0.92),
            minSide: 1600);
        certLower = DownscaleForOcr(certLower, Math.Min(3200, _flatArtworkMaxOcrSide + 600));
        crops.Add(_preprocessor.PreprocessFlatLabel(certLower));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(certLower));
        crops.Add(_preprocessor.PreprocessGrayscaleRaw(certLower));

        var originBand = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.38, 0.78),
            minSide: 1600);
        originBand = DownscaleForOcr(originBand, Math.Min(3200, _flatArtworkMaxOcrSide + 600));
        crops.Add(_preprocessor.PreprocessFlatLabel(originBand));
        crops.Add(_preprocessor.PreprocessWhiteOnDarkLabel(originBand));

        var importerBand = _preprocessor.UpscaleIfSmall(
            _preprocessor.CropBand(rawCertPage, 0.42, 0.72),
            minSide: 1600);
        importerBand = DownscaleForOcr(importerBand, Math.Min(3200, _flatArtworkMaxOcrSide + 600));
        crops.Add(_preprocessor.PreprocessFlatLabel(importerBand));
        crops.Add(_preprocessor.PreprocessLineArtFlatLabel(importerBand));

        return crops;
    }

    public Task<double> GetBoldConfidenceAsync(byte[] imageBytes, string phrase, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var footer = _preprocessor.CropBand(imageBytes, 0.62, 1.0);
            if (_preprocessor.ClassifyImage(imageBytes) == LabelImageKind.FlatArtwork)
            {
                return _preprocessor.EstimateFlatBoldTypographyConfidence(footer);
            }

            return _preprocessor.EstimateBoldPhraseConfidence(footer, phrase);
        }, cancellationToken);
    }

    private string RunPass(byte[] processedCrop, PageSegMode mode)
    {
        try
        {
            using var lease = _engineProvider.RentEngine();
            var engine = lease.Engine;
            using var pix = Pix.LoadFromMemory(processedCrop);
            engine.DefaultPageSegMode = mode;
            using var page = engine.Process(pix);
            return page.GetText() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string MergeOcrCorpus(IEnumerable<string> passes)
    {
        var lines = passes
            .SelectMany(p => p.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(l => l.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", lines);
    }
}
