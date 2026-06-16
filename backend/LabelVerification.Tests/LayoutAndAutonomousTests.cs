using System.Runtime.InteropServices;
using LabelVerification.Core.Extraction;
using LabelVerification.Core.Layout;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Tests;

internal static class SamplePaths
{
    internal static string FindPlanRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "testdata", "colas")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root.");
    }

    internal static string ResolveSamplePath(string fileName)
    {
        var root = FindPlanRepoRoot();
        var publicPath = Path.Combine(root, "public", "samples", fileName);
        if (File.Exists(publicPath))
        {
            return publicPath;
        }

        var testPath = Path.Combine(root, "testdata", "reviewer-pack", fileName);
        if (File.Exists(testPath))
        {
            return testPath;
        }

        throw new FileNotFoundException($"Sample not found: {fileName}");
    }

    internal static string FindTessDataRoot()
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

[Collection(nameof(OcrSequentialCollection))]
public class LayoutRoiQualityTests
{
    [Theory]
    [InlineData("05-odp-jack-daniels-old-no7.png", 0.45)]
    [InlineData("04-odp-la-venenosa-raicilla.png", 0.40)]
    [InlineData("03-odp-ambhar-plata.png", 0.40)]
    public async Task Annotated_sample_layout_rois_meet_iou_threshold(string fileName, double minMeanIoU)
    {
        if (TestPlatform.IsWindowsDev && fileName is not "05-odp-jack-daniels-old-no7.png")
        {
            return;
        }

        await using var provider = BuildProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var store = provider.GetRequiredService<ILayoutAnnotationStore>();
        Assert.True(store.TryGetSample(fileName, out var sample), $"Missing annotation for {fileName}");

        var imagePath = SamplePaths.ResolveSamplePath(fileName);
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var detection = provider.GetRequiredService<ILabelLayoutDetector>().Detect(bytes);
        var report = LayoutRoiMetrics.EvaluateSample(sample!, detection);

        Assert.True(
            report.MeanIoU >= minMeanIoU,
            $"{fileName} mean IoU {report.MeanIoU:0.###} below {minMeanIoU:0.###}. Fields: {string.Join(", ", report.Fields.Select(f => $"{f.Field}={f.IoU:0.##}"))}");
    }

    private static ServiceProvider BuildProvider()
    {
        var repoRoot = SamplePaths.FindTessDataRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "5",
                ["Ocr:SubmissionGradeTargetMs"] = "2000",
                ["Layout:PreferOnnx"] = "false",
                ["Layout:AnnotationsDir"] = Path.Combine(SamplePaths.FindPlanRepoRoot(), "testdata", "layout-annotations"),
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
    }
}

[Collection(nameof(OcrSequentialCollection))]
public class AutonomousVerificationTests
{
    [Fact]
    public async Task Jack_autonomous_verify_passes_without_expected_fields()
    {
        if (TestPlatform.IsWindowsDev)
        {
            return;
        }

        await using var provider = BuildProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var imagePath = SamplePaths.ResolveSamplePath("05-odp-jack-daniels-old-no7.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var result = await provider.GetRequiredService<IAutonomousVerificationService>().VerifyAsync(bytes);

        Assert.True(result.ColaRegistryHit, "Jack TTB ID should resolve in COLA cache.");
        Assert.Equal("13343001000271", result.ResolvedTtbId);
        Assert.False(string.IsNullOrWhiteSpace(result.Extraction.Fields.BrandName));
        Assert.True(
            result.Verification.OverallStatus == VerificationOutcome.Pass,
            $"Autonomous Jack verify failed: {string.Join(", ", result.Verification.Fields.Where(f => !f.IsMatch).Select(f => f.FieldName))}");
    }

    [Fact]
    public async Task Extract_returns_ttb_id_from_jack_certificate()
    {
        if (TestPlatform.IsWindowsDev)
        {
            return;
        }

        await using var provider = BuildProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var imagePath = SamplePaths.ResolveSamplePath("05-odp-jack-daniels-old-no7.png");
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var extraction = await provider.GetRequiredService<ILabelFieldExtractor>().ExtractAsync(bytes);

        Assert.Equal("13343001000271", extraction.Fields.TtbId);
        Assert.NotNull(extraction.Fields.AbvPercent);
        Assert.False(string.IsNullOrWhiteSpace(extraction.Fields.NetContents));
    }

    private static ServiceProvider BuildProvider()
    {
        var repoRoot = SamplePaths.FindTessDataRoot();
        var planRoot = SamplePaths.FindPlanRepoRoot();
        var enginePoolSize = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "1" : "6";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "8",
                ["Ocr:FlatArtworkEnginePoolSize"] = enginePoolSize,
                ["Ocr:FlatArtworkMaxOcrSide"] = "1200",
                ["Ocr:UseFieldBandTargetedOcr"] = "true",
                ["Ocr:PerLabelWallClockMs"] = "8000",
                ["Ocr:SubmissionGradeTargetMs"] = "3500",
                ["Cola:ColasDir"] = Path.Combine(planRoot, "testdata", "colas"),
                ["Layout:PreferOnnx"] = "false",
                ["Layout:AnnotationsDir"] = Path.Combine(planRoot, "testdata", "layout-annotations"),
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
    }
}
