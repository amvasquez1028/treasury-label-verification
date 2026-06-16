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
public class ReviewerPackSampleTests(OcrEngineFixture ocrFixture)
{
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
        foreach (var item in manifest
                     .Where(i => i.SampleKind == "odp_approved_label")
                     .OrderBy(i => i.File == "05-odp-jack-daniels-old-no7.png" ? 0 : 1)
                     .ThenBy(i => i.File))
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
    public async Task Odp_approved_label_sample_passes_verification(
        string fileName,
        string imagePath,
        ReviewerPackManifestItem manifestItem)
    {
        if (TestPlatform.IsWindowsDev && fileName is not "05-odp-jack-daniels-old-no7.png")
        {
            return;
        }

        var provider = ocrFixture.Provider;

        Assert.NotNull(manifestItem.ExpectedLabelFields);
        var expected = manifestItem.ExpectedLabelFields with
        {
            LabelPresentation = LabelPresentation.FullLabel
        };

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var service = provider.GetRequiredService<IVerificationService>();
        var result = await service.VerifyAsync(imageBytes, expected);

        Assert.False(string.IsNullOrWhiteSpace(result.RawOcrText), $"{fileName} produced no OCR text.");

        var failedFields = result.Fields.Where(f => !f.IsMatch).Select(f => $"{f.FieldName} conf={f.Confidence}").ToList();
        var fieldSummary = string.Join(", ", result.Fields.Select(f => $"{f.FieldName}={f.IsMatch}/{f.Confidence:0.####}"));
        var failureSummary =
            $"status={result.OverallStatus}; failed=[{string.Join(", ", failedFields)}]; fields=[{fieldSummary}]; ocr={result.RawOcrText[..Math.Min(240, result.RawOcrText.Length)]}";
        Assert.True(result.OverallStatus == VerificationOutcome.Pass, failureSummary);
        Assert.True(result.IsVerified, $"{fileName} ({manifestItem.TtbId}) should pass against Texas ODP label artwork.");
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

    public sealed record ReviewerPackManifestItem
    {
        public required string File { get; init; }
        public required string TtbId { get; init; }
        public string? SampleKind { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
