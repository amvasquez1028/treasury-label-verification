using System.Diagnostics;
using System.Text.Json;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Tests;

public class VerificationFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ServiceProvider BuildProviderForTests()
    {
        var repoRoot = FindRepoRoot();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
                ["Ocr:TimeoutSeconds"] = "12"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddLabelVerificationCore(config);
        return services.BuildServiceProvider();
    }

    public static IEnumerable<object[]> FixtureFiles()
    {
        var fixturesDir = Path.Combine(FindRepoRoot(), "testdata", "fixtures");
        if (!Directory.Exists(fixturesDir))
        {
            yield break;
        }

        foreach (var jsonPath in Directory.GetFiles(fixturesDir, "*.json"))
        {
            var baseName = Path.GetFileNameWithoutExtension(jsonPath);
            if (baseName.StartsWith("unreadable-", StringComparison.Ordinal))
            {
                continue;
            }

            var imagePath = Path.Combine(fixturesDir, $"{baseName}.png");
            if (File.Exists(imagePath))
            {
                yield return new object[] { baseName, imagePath, jsonPath };
            }
        }
    }

    public static IEnumerable<object[]> UnreadableFixtureFiles()
    {
        var fixturesDir = Path.Combine(FindRepoRoot(), "testdata", "fixtures");
        if (!Directory.Exists(fixturesDir))
        {
            yield break;
        }

        foreach (var jsonPath in Directory.GetFiles(fixturesDir, "unreadable-*.json"))
        {
            var baseName = Path.GetFileNameWithoutExtension(jsonPath);
            var imagePath = Path.Combine(fixturesDir, $"{baseName}.png");
            if (File.Exists(imagePath))
            {
                yield return new object[] { baseName, imagePath, jsonPath };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public async Task Fixture_matches_expected_fields(string name, string imagePath, string expectedPath)
    {
        await using var provider = BuildProviderForTests();
        var engine = provider.GetRequiredService<ITesseractEngineProvider>();
        engine.WarmUp();

        var expectedJson = await File.ReadAllTextAsync(expectedPath);
        var expected = JsonSerializer.Deserialize<ExpectedLabelFields>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid fixture JSON: {expectedPath}");

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var service = provider.GetRequiredService<IVerificationService>();
        var sw = Stopwatch.StartNew();
        var result = await service.VerifyAsync(imageBytes, expected);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, $"{name} exceeded 5000ms perf gate ({sw.ElapsedMilliseconds}ms)");
        Assert.True(result.Fields.All(f => f.IsMatch), $"{name} field mismatch. OCR: {result.RawOcrText}");
        Assert.True(
            result.OverallStatus is VerificationOutcome.Pass or VerificationOutcome.Review,
            $"{name} unexpected status {result.OverallStatus}");
        Assert.True(result.OverallConfidence >= 0.75, $"{name} confidence too low: {result.OverallConfidence}");
    }

    [Theory]
    [MemberData(nameof(UnreadableFixtureFiles))]
    public async Task Unreadable_fixture_returns_unreadable_status(string name, string imagePath, string expectedPath)
    {
        await using var provider = BuildProviderForTests();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var expectedJson = await File.ReadAllTextAsync(expectedPath);
        var expected = JsonSerializer.Deserialize<ExpectedLabelFields>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid fixture JSON: {expectedPath}");

        var service = provider.GetRequiredService<IVerificationService>();
        var result = await service.VerifyAsync(await File.ReadAllBytesAsync(imagePath), expected);

        Assert.True(
            result.OverallStatus == VerificationOutcome.Unreadable,
            $"{name} expected unreadable status but was {result.OverallStatus}");
        Assert.False(result.IsVerified);
        Assert.NotEmpty(result.Fields);
        Assert.NotNull(result.StatusMessage);
        Assert.NotNull(result.AgentGuidance);
    }

    [Fact]
    public async Task Baseline_fixture_meets_perf_gate()
    {
        var fixturesDir = Path.Combine(FindRepoRoot(), "testdata", "fixtures");
        var baseline = Path.Combine(fixturesDir, "baseline-01.png");
        if (!File.Exists(baseline))
        {
            return;
        }

        await using var provider = BuildProviderForTests();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();
        var expected = JsonSerializer.Deserialize<ExpectedLabelFields>(
            await File.ReadAllTextAsync(Path.Combine(fixturesDir, "baseline-01.json")),
            JsonOptions)!;

        var sw = Stopwatch.StartNew();
        _ = await provider.GetRequiredService<IVerificationService>()
            .VerifyAsync(await File.ReadAllBytesAsync(baseline), expected);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000);
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
}
