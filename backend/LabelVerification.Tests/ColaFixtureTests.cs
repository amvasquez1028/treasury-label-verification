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
public class ColaFixtureTests(OcrEngineFixture ocrFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static IEnumerable<object[]> ColaFixtureFiles()
    {
        var colasDir = Path.Combine(FindRepoRoot(), "testdata", "colas");
        if (!Directory.Exists(colasDir))
        {
            yield break;
        }

        foreach (var metaPath in Directory.GetFiles(colasDir, "*.meta.json"))
        {
            var metaJson = File.ReadAllText(metaPath);
            var meta = JsonSerializer.Deserialize<ColaFixtureMeta>(metaJson, JsonOptions);
            if (meta is null)
            {
                continue;
            }

            var ttbId = meta.TtbId;
            var imagePath = Path.Combine(colasDir, $"{ttbId}.png");
            if (File.Exists(imagePath))
            {
                yield return new object[] { ttbId, imagePath, metaPath };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ColaFixtureFiles))]
    public async Task Cola_fixture_matches_expected_fields(string ttbId, string imagePath, string metaPath)
    {
        var metaJson = await File.ReadAllTextAsync(metaPath);
        var meta = JsonSerializer.Deserialize<ColaFixtureMeta>(metaJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid COLA meta JSON: {metaPath}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsRealOrSyntheticBottleFixture(meta))
        {
            return;
        }

        await using var provider = OcrEngineFixture.BuildProvider(new Dictionary<string, string?>
        {
            ["Ocr:TimeoutSeconds"] = "12",
            ["Ocr:PerLabelWallClockMs"] = "12000",
        });
        var engine = provider.GetRequiredService<ITesseractEngineProvider>();
        engine.WarmUp();

        Assert.Equal(ttbId, meta.TtbId);
        Assert.NotNull(meta.ExpectedLabelFields);
        var expected = MergeFancifulName(meta.ExpectedLabelFields, meta.FancifulName);

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var service = provider.GetRequiredService<IVerificationService>();
        var sw = Stopwatch.StartNew();
        var result = await service.VerifyAsync(imageBytes, expected);
        sw.Stop();

        var isRealPhoto = IsRealOrSyntheticBottleFixture(meta);
        var shouldPass = meta.ExpectVerificationPass == true || meta.ImageSource == "synthetic_bottle";
        var isFrontLabelPartialPass = meta.ImageSource == "tabc_csv_real_photo" && meta.ExpectVerificationPass == false;

        var perfLimitMs = isRealPhoto ? 15000 : 5000;
        Assert.True(sw.ElapsedMilliseconds < perfLimitMs, $"COLA {ttbId} exceeded {perfLimitMs}ms perf gate ({sw.ElapsedMilliseconds}ms)");

        Assert.False(string.IsNullOrWhiteSpace(result.RawOcrText), $"COLA {ttbId} produced no OCR text.");

        if (isFrontLabelPartialPass)
        {
            Assert.False(result.IsVerified, $"COLA {ttbId} should fail overall on front-label photo missing warnings.");
            Assert.Equal(VerificationOutcome.Fail, result.OverallStatus);

            foreach (var fieldName in meta.ExpectedFieldsToFail ?? ["TtbWarningText", "BoldWarningPhrase"])
            {
                var field = result.Fields.FirstOrDefault(f => f.FieldName == fieldName);
                Assert.NotNull(field);
                Assert.False(field.IsMatch, $"COLA {ttbId} expected {fieldName} to fail on front photo.");
            }

            foreach (var fieldName in meta.ExpectedFieldsToPass ?? [])
            {
                var field = result.Fields.FirstOrDefault(f => f.FieldName == fieldName);
                Assert.NotNull(field);
                Assert.True(field.IsMatch, $"COLA {ttbId} expected {fieldName} to pass. OCR: {result.RawOcrText}");
            }

            if (ttbId == "14086001000323")
            {
                var normalizedOcr = result.RawOcrText.ToUpperInvariant();
                Assert.True(
                    normalizedOcr.Contains("VENENOSA", StringComparison.Ordinal)
                        || normalizedOcr.Contains("OCCIDENTAL", StringComparison.Ordinal)
                        || normalizedOcr.Contains("RAICILLA", StringComparison.Ordinal),
                    $"COLA {ttbId} expected partial label text in OCR: {result.RawOcrText}");
            }

            return;
        }

        if (shouldPass)
        {
            Assert.True(
                result.IsVerified || result.OverallStatus == VerificationOutcome.Review,
                $"COLA {ttbId} expected pass/review but got {result.OverallStatus}. OCR: {result.RawOcrText}");
            Assert.True(result.OverallConfidence >= 0.65, $"COLA {ttbId} confidence too low: {result.OverallConfidence}");
            return;
        }

        if (meta.ImageSource is "wikimedia_commons" or "manual_or_existing")
        {
            return;
        }

        Assert.True(result.IsVerified, $"COLA {ttbId} failed verification. OCR: {result.RawOcrText}");
        Assert.True(result.OverallConfidence >= 0.75, $"COLA {ttbId} confidence too low: {result.OverallConfidence}");
    }

    [Fact]
    public void Cola_fixture_suite_includes_registry_examples()
    {
        var colasDir = Path.Combine(FindRepoRoot(), "testdata", "colas");
        if (!Directory.Exists(colasDir))
        {
            return;
        }

        var expectedIds = new[]
        {
            "03211001000018",
            "11115001000373",
            "11364001000181",
            "12207001000536",
            "15107001000276",
            "20085001000218",
            "13297001000322",
            "14106001000237",
            "18303001000896",
            "21194001000323",
            "18055001000023",
            "14086001000323",
            "13343001000271",
        };

        foreach (var id in expectedIds)
        {
            Assert.True(File.Exists(Path.Combine(colasDir, $"{id}.meta.json")), $"Missing {id}.meta.json");
            Assert.True(File.Exists(Path.Combine(colasDir, $"{id}.png")), $"Missing {id}.png");
        }
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
        [JsonPropertyName("ttbId")]
        public required string TtbId { get; init; }

        [JsonPropertyName("fancifulName")]
        public string? FancifulName { get; init; }

        [JsonPropertyName("expectedLabelFields")]
        public required ExpectedLabelFields ExpectedLabelFields { get; init; }

        [JsonPropertyName("imageSource")]
        public string? ImageSource { get; init; }

        [JsonPropertyName("expectVerificationPass")]
        public bool? ExpectVerificationPass { get; init; }

        [JsonPropertyName("expectedFieldsToPass")]
        public IReadOnlyList<string>? ExpectedFieldsToPass { get; init; }

        [JsonPropertyName("expectedFieldsToFail")]
        public IReadOnlyList<string>? ExpectedFieldsToFail { get; init; }
    }

    private static bool IsRealOrSyntheticBottleFixture(ColaFixtureMeta meta) =>
        meta.ImageSource is "wikimedia_commons"
            or "manual_or_existing"
            or "real_bottle_photo"
            or "synthetic_bottle"
            or "user_provided_real_photo"
            or "tabc_csv_real_photo";

    private static ExpectedLabelFields MergeFancifulName(ExpectedLabelFields expected, string? fancifulName)
    {
        if (string.IsNullOrWhiteSpace(expected.FancifulName) && !string.IsNullOrWhiteSpace(fancifulName))
        {
            return expected with { FancifulName = fancifulName.Trim() };
        }

        return expected;
    }
}
