using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Models;
using LabelVerification.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class ReviewerPackMismatchTests(OcrEngineFixture ocrFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IEnumerable<object[]> MismatchSamples()
    {
        var planRoot = LabelVerifyWebFactory.FindPlanRepoRoot();
        var manifestPath = Path.Combine(planRoot, "public", "samples", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            yield break;
        }

        var manifest = JsonSerializer.Deserialize<List<ManifestItem>>(
            File.ReadAllText(manifestPath),
            JsonOptions) ?? [];

        var samplesDir = Path.Combine(planRoot, "public", "samples");
        foreach (var item in manifest.Where(i => i.SampleKind == "mismatch_bottle_photo"))
        {
            var imagePath = Path.Combine(samplesDir, item.File);
            if (File.Exists(imagePath) && item.ExpectedLabelFields is not null)
            {
                yield return new object[] { item.File, imagePath, item.ExpectedLabelFields };
            }
        }
    }

    [Theory]
    [MemberData(nameof(MismatchSamples))]
    public async Task Mismatch_bottle_photo_fails_against_application_values(
        string fileName,
        string imagePath,
        ExpectedLabelFields expectedFromManifest)
    {
        var service = ocrFixture.Provider.GetRequiredService<IVerificationService>();
        var result = await service.VerifyAsync(await File.ReadAllBytesAsync(imagePath), expectedFromManifest);

        Assert.False(string.IsNullOrWhiteSpace(result.RawOcrText), $"{fileName} should produce OCR text.");
        Assert.False(result.IsVerified, $"{fileName} should fail — photo does not match application metadata.");
        Assert.Equal(VerificationOutcome.Fail, result.OverallStatus);
    }

    private sealed record ManifestItem
    {
        public required string File { get; init; }
        public string? SampleKind { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
