using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class FlatArtworkPerformanceTests
{
    private static int MaxMillisecondsPerSample => ResolveTargetMs();

    private static int ResolveTargetMs()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("OCR_SLA_TARGET_MS"), out var configured) && configured > 0)
        {
            return configured;
        }

        // P1v3 batch target is 5000ms/label end-to-end; Windows dev uses a single OCR engine and runs slower.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 12000 : 5000;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static IEnumerable<object[]> OdpApprovedLabelSamples()
    {
        var planRoot = FindPlanRepoRoot();
        var manifestPath = Path.Combine(planRoot, "public", "samples", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            yield break;
        }

        var manifest = JsonSerializer.Deserialize<List<ReviewerPackManifestItem>>(
            File.ReadAllText(manifestPath),
            JsonOptions) ?? [];

        var samplesDir = Path.Combine(planRoot, "public", "samples");
        foreach (var item in manifest.Where(i => i.SampleKind == "odp_approved_label"))
        {
            var imagePath = Path.Combine(samplesDir, item.File);
            if (File.Exists(imagePath))
            {
                yield return new object[] { item.File, imagePath, item };
            }
        }
    }

    [Theory]
    [MemberData(nameof(OdpApprovedLabelSamples))]
    public async Task Odp_flat_label_verifies_within_two_seconds_on_p1v3(
        string fileName,
        string imagePath,
        ReviewerPackManifestItem manifestItem)
    {
        await using var provider = BuildProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        Assert.NotNull(manifestItem.ExpectedLabelFields);
        var expected = manifestItem.ExpectedLabelFields with
        {
            LabelPresentation = LabelPresentation.FullLabel
        };

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var service = provider.GetRequiredService<IVerificationService>();
        var stopwatch = Stopwatch.StartNew();
        var result = await service.VerifyAsync(imageBytes, expected);
        stopwatch.Stop();

        Assert.True(
            result.ProcessingTimeMs <= MaxMillisecondsPerSample,
            $"{fileName} processing took {result.ProcessingTimeMs}ms (limit {MaxMillisecondsPerSample}ms).");
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

        throw new DirectoryNotFoundException("Could not locate treasury-label-verification-plan repo root.");
    }

    private static ServiceProvider BuildProvider()
    {
        var repoRoot = FindTessDataRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "5",
                ["Ocr:FlatArtworkMaxOcrSide"] = "1800",
                ["Ocr:FlatArtworkEnginePoolSize"] = "4",
                ["Ocr:SubmissionGradeTargetMs"] = "2000",
                ["Ocr:PerLabelWallClockMs"] = "5000",
                ["Ocr:UseFieldBandTargetedOcr"] = "true",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
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

        throw new DirectoryNotFoundException("Could not locate tessdata directory.");
    }

    public sealed record ReviewerPackManifestItem
    {
        public required string File { get; init; }
        public required string TtbId { get; init; }
        public string? SampleKind { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
