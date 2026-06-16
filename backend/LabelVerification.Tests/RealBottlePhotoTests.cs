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
public class RealBottlePhotoTests(OcrEngineFixture ocrFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task Johnnie_walker_real_photo_composite_extracts_regulatory_sticker_text()
    {
        var repoRoot = FindRepoRoot();
        var imagePath = Path.Combine(repoRoot, "testdata", "colas", "20085001000218-real-composite.png");
        var metaPath = Path.Combine(repoRoot, "testdata", "colas", "20085001000218.meta.json");

        if (!File.Exists(imagePath) || !File.Exists(metaPath))
        {
            return;
        }

        var metaJson = await File.ReadAllTextAsync(metaPath);
        var meta = JsonSerializer.Deserialize<ColaFixtureMeta>(metaJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid COLA meta JSON: {metaPath}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var expected = MergeFancifulName(meta.ExpectedLabelFields, meta.FancifulName);
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        await using var provider = OcrEngineFixture.BuildProvider(new Dictionary<string, string?>
        {
            ["Ocr:TimeoutSeconds"] = "12",
            ["Ocr:PerLabelWallClockMs"] = "12000",
        });
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();
        var result = await provider.GetRequiredService<IVerificationService>().VerifyAsync(imageBytes, expected);

        Assert.False(string.IsNullOrWhiteSpace(result.RawOcrText));
        var normalizedOcr = result.RawOcrText.ToUpperInvariant();
        Assert.True(
            normalizedOcr.Contains("YEAR OF THE TIGER", StringComparison.Ordinal)
                || normalizedOcr.Contains("YEAR OF THE", StringComparison.Ordinal),
            $"Expected Year of the Tiger sticker text in OCR: {result.RawOcrText}");
    }

    private static string FindRepoRoot()
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

        throw new DirectoryNotFoundException("Could not locate repo root with tessdata/");
    }

    private sealed record ColaFixtureMeta
    {
        [JsonPropertyName("expectedLabelFields")]
        public required ExpectedLabelFields ExpectedLabelFields { get; init; }

        [JsonPropertyName("fancifulName")]
        public string? FancifulName { get; init; }
    }

    private static ExpectedLabelFields MergeFancifulName(ExpectedLabelFields expected, string? fancifulName)
    {
        if (string.IsNullOrWhiteSpace(expected.FancifulName) && !string.IsNullOrWhiteSpace(fancifulName))
        {
            return expected with { FancifulName = fancifulName.Trim() };
        }

        return expected;
    }
}
