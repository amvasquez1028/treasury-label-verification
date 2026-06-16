using LabelVerification.Core.Compliance;
using LabelVerification.Core.Layout;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Tesseract;

namespace LabelVerification.Core.Extraction;

internal static class LayoutRegionCropper
{
    internal static byte[] CropRegion(byte[] imageBytes, LabelFieldRegion region, IImagePreprocessor preprocessor)
    {
        var pages = region.PageIndex > 0 || IsStacked(imageBytes)
            ? StackedTabcPageHelper.Split(imageBytes)
            : (IReadOnlyList<byte[]>)[imageBytes];

        var pageIndex = Math.Clamp(region.PageIndex, 0, Math.Max(0, pages.Count - 1));
        var pageBytes = pages[pageIndex];
        return preprocessor.CropBand(pageBytes, region.Y, region.Bottom);
    }

    internal static byte[] CropRegionWithHorizontalBounds(
        byte[] imageBytes,
        LabelFieldRegion region,
        IImagePreprocessor preprocessor)
    {
        using var page = Mat.FromImageData(CropRegion(imageBytes, region, preprocessor), ImreadModes.Color);
        var x = (int)(region.X * page.Width);
        var w = (int)(region.Width * page.Width);
        x = Math.Clamp(x, 0, Math.Max(0, page.Width - 1));
        w = Math.Clamp(w, 1, page.Width - x);
        using var roi = new Mat(page, new OpenCvSharp.Rect(x, 0, w, page.Height));
        return roi.ToBytes(".png");
    }

    private static bool IsStacked(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        return mat.Height / (double)Math.Max(1, mat.Width) > 2.0;
    }
}

public interface ILayoutGuidedOcrService
{
    Task<IReadOnlyDictionary<LayoutFieldKind, string>> ExtractRegionTextsAsync(
        byte[] imageBytes,
        LayoutDetectionResult layout,
        CancellationToken cancellationToken);
}

internal sealed class LayoutGuidedOcrService : ILayoutGuidedOcrService
{
    private readonly ITesseractEngineProvider _engineProvider;
    private readonly IImagePreprocessor _preprocessor;
    private readonly LayoutOptions _layoutOptions;
    private readonly OcrOptions _ocrOptions;

    public LayoutGuidedOcrService(
        ITesseractEngineProvider engineProvider,
        IImagePreprocessor preprocessor,
        IOptions<LayoutOptions> layoutOptions,
        IOptions<OcrOptions> ocrOptions)
    {
        _engineProvider = engineProvider;
        _preprocessor = preprocessor;
        _layoutOptions = layoutOptions.Value;
        _ocrOptions = ocrOptions.Value;
    }

    public Task<IReadOnlyDictionary<LayoutFieldKind, string>> ExtractRegionTextsAsync(
        byte[] imageBytes,
        LayoutDetectionResult layout,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var budgetMs = Math.Min(_layoutOptions.RoiOcrBudgetMs, _ocrOptions.SubmissionGradeTargetMs);
            var results = new Dictionary<LayoutFieldKind, string>();
            var ordered = layout.Regions
                .OrderByDescending(r => r.Field == LayoutFieldKind.CertificateHeader)
                .ThenByDescending(r => r.Field == LayoutFieldKind.GovernmentWarning)
                .ThenByDescending(r => r.Confidence)
                .ToList();

            foreach (var region in ordered)
            {
                if (timer.ElapsedMilliseconds >= budgetMs)
                {
                    break;
                }

                var crop = LayoutRegionCropper.CropRegionWithHorizontalBounds(imageBytes, region, _preprocessor);
                crop = SelectPreprocess(region.Field, crop);
                var text = RunOcr(crop);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results[region.Field] = results.TryGetValue(region.Field, out var existing)
                        ? $"{existing}\n{text}"
                        : text;
                }
            }

            return (IReadOnlyDictionary<LayoutFieldKind, string>)results;
        }, cancellationToken);
    }

    private byte[] SelectPreprocess(LayoutFieldKind field, byte[] crop)
    {
        return field switch
        {
            LayoutFieldKind.CertificateHeader => _preprocessor.PreprocessGrayscaleRaw(crop),
            LayoutFieldKind.GovernmentWarning => _preprocessor.PreprocessFlatLabel(crop),
            LayoutFieldKind.BrandBlock => _preprocessor.IsDarkLabelArtwork(crop)
                ? _preprocessor.PreprocessWhiteOnDarkLabel(crop)
                : _preprocessor.PreprocessFlatLabel(crop),
            _ => _preprocessor.PreprocessFlatLabel(crop),
        };
    }

    private string RunOcr(byte[] crop)
    {
        try
        {
            using var lease = _engineProvider.RentEngine();
            using var pix = Pix.LoadFromMemory(crop);
            lease.Engine.DefaultPageSegMode = PageSegMode.SingleBlock;
            using var page = lease.Engine.Process(pix);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
