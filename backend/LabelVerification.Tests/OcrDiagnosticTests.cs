using System.Text.RegularExpressions;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using Tesseract;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class OcrDiagnosticTests
{
    [Fact]
    public async Task Jack_certificate_band_ocr_includes_size()
    {
        await using var provider = BuildProvider(submissionGradeTargetMs: 15000);
        var engineProvider = provider.GetRequiredService<ITesseractEngineProvider>();
        engineProvider.WarmUp();
        var preprocessor = provider.GetRequiredService<IImagePreprocessor>();

        var imagePath = Path.Combine(FindPlanRepoRoot(), "public", "samples", "05-odp-jack-daniels-old-no7.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        using var mat = Mat.FromImageData(bytes, ImreadModes.Color);
        var bandTexts = new List<string>();

        foreach (var (top, bottom) in new (double, double)[]
        {
            (0.0, 0.10),
            (0.0, 0.14),
            (0.0, 0.16),
            (0.0, 0.20),
        })
        {
            var band = preprocessor.CropBand(bytes, top, bottom);
            band = preprocessor.UpscaleIfSmall(band, minSide: 1600);
            bandTexts.Add(await RunOcr(engineProvider, band, PageSegMode.SingleBlock));
            bandTexts.Add(await RunOcr(engineProvider, preprocessor.PreprocessGrayscaleRaw(band), PageSegMode.SingleBlock));
        }

        var merged = string.Join("\n", bandTexts);
        Assert.True(
            merged.Contains("375", StringComparison.OrdinalIgnoreCase),
            $"Certificate OCR missing 375 ({mat.Width}x{mat.Height}). Snippet: {merged[..Math.Min(800, merged.Length)]}");
    }

    [Fact]
    public async Task Jack_full_budget_ocr_includes_net_contents()
    {
        await using var provider = BuildProvider(submissionGradeTargetMs: 15000);
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var imagePath = Path.Combine(FindPlanRepoRoot(), "public", "samples", "05-odp-jack-daniels-old-no7.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var text = await provider.GetRequiredService<IOcrService>().ExtractTextAsync(bytes, default);

        Assert.True(
            text.Contains("375", StringComparison.OrdinalIgnoreCase),
            $"OCR missing 375. Snippet: {text[..Math.Min(800, text.Length)]}");
    }

    private static async Task<string> RunOcr(
        ITesseractEngineProvider engineProvider,
        byte[] crop,
        PageSegMode mode)
    {
        return await Task.Run(() =>
        {
            using var lease = engineProvider.RentEngine();
            using var pix = Pix.LoadFromMemory(crop);
            lease.Engine.DefaultPageSegMode = mode;
            using var page = lease.Engine.Process(pix);
            return page.GetText() ?? string.Empty;
        });
    }

    [Fact]
    public async Task Ambhar_full_budget_ocr_includes_brand_and_net()
    {
        await using var provider = BuildProductionLikeProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var imagePath = Path.Combine(FindPlanRepoRoot(), "public", "samples", "03-odp-ambhar-plata.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var text = await provider.GetRequiredService<IOcrService>().ExtractTextAsync(bytes, default);

        Assert.True(Regex.IsMatch(text, "A.?MBHAR", RegexOptions.IgnoreCase));
        Assert.True(
            text.Contains("750", StringComparison.OrdinalIgnoreCase)
                || text.Contains("40", StringComparison.OrdinalIgnoreCase),
            $"OCR missing net/abv cert metadata. Snippet: {text[..Math.Min(800, text.Length)]}");
    }

    [Fact]
    public async Task Venenosa_full_budget_ocr_includes_brand_and_net()
    {
        await using var provider = BuildProductionLikeProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var imagePath = Path.Combine(FindPlanRepoRoot(), "public", "samples", "04-odp-la-venenosa-raicilla.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var text = await provider.GetRequiredService<IOcrService>().ExtractTextAsync(bytes, default);

        Assert.Contains("VENENOSA", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            text.Contains("750", StringComparison.OrdinalIgnoreCase),
            $"OCR missing 750. Snippet: {text[..Math.Min(800, text.Length)]}");
    }

    private static ServiceProvider BuildProductionLikeProvider()
    {
        var repoRoot = FindTessDataRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "15",
                ["Ocr:SubmissionGradeTargetMs"] = "6000",
                ["Ocr:PerLabelWallClockMs"] = "15000",
                ["Ocr:UseFieldBandTargetedOcr"] = "true",
                ["Ocr:FlatArtworkMaxOcrSide"] = "1200",
                ["Ocr:FlatArtworkEnginePoolSize"] = "1",
            })
            .Build();

        return new ServiceCollection()
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
    }

    private static ServiceProvider BuildProvider(int submissionGradeTargetMs)
    {
        var repoRoot = FindTessDataRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "30",
                ["Ocr:SubmissionGradeTargetMs"] = submissionGradeTargetMs.ToString(),
                ["Ocr:PerLabelWallClockMs"] = "15000",
                ["Ocr:UseFieldBandTargetedOcr"] = "true",
                ["Ocr:FlatArtworkMaxOcrSide"] = "1800",
            })
            .Build();

        return new ServiceCollection()
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
    }

    private static string FindPlanRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "package.json"))
                && Directory.Exists(Path.Combine(dir.FullName, "public", "samples")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root.");
    }

    private static string FindTessDataRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tessdata")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tessdata.");
    }
}
