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
public class JackDanielsVerificationTests
{
    [Fact]
    public async Task Jack_odp_approved_label_passes_full_verification()
    {
        await using var provider = BuildProvider();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var planRoot = FindPlanRepoRoot();
        var manifest = JsonSerializer.Deserialize<List<ReviewerPackManifestItem>>(
            await File.ReadAllTextAsync(Path.Combine(planRoot, "public", "samples", "manifest.json")),
            JsonOptions) ?? [];

        var item = manifest.Single(i => i.File == "05-odp-jack-daniels-old-no7.png");
        Assert.NotNull(item.ExpectedLabelFields);

        var expected = item.ExpectedLabelFields with { LabelPresentation = LabelPresentation.FullLabel };
        var imageBytes = await File.ReadAllBytesAsync(Path.Combine(planRoot, "public", "samples", item.File));
        var result = await provider.GetRequiredService<IVerificationService>().VerifyAsync(imageBytes, expected);

        var failedFields = result.Fields.Where(f => !f.IsMatch).Select(f => f.FieldName).ToList();
        Assert.True(
            result.OverallStatus == VerificationOutcome.Pass,
            $"Jack should pass. Failed: [{string.Join(", ", failedFields)}].");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static ServiceProvider BuildProvider()
    {
        var repoRoot = FindTessDataRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "25",
                ["Ocr:FlatArtworkMaxOcrSide"] = "1800",
                ["Ocr:FlatArtworkEnginePoolSize"] = "4",
                ["Ocr:SubmissionGradeTargetMs"] = "2000",
                ["Ocr:PerLabelWallClockMs"] = "15000",
                ["Ocr:SubmissionGradeSupplementWallClockMs"] = "6000",
                ["Ocr:UseFieldBandTargetedOcr"] = "true",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
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

    private sealed record ReviewerPackManifestItem
    {
        public required string File { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
