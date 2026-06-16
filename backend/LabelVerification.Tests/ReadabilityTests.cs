using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using LabelVerification.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace LabelVerification.Tests;

public class ReadabilityTests
{
    [Fact]
    public void Assess_blank_image_with_empty_ocr_is_unreadable()
    {
        var assessor = BuildAssessor();
        var blankPng = CreateUniformPng(900, 1200, 250);

        var assessment = assessor.Assess(blankPng, string.Empty);

        Assert.False(assessment.IsReadable);
        Assert.NotNull(assessment.Reason);
        Assert.NotNull(assessment.AgentGuidance);
    }

    [Fact]
    public async Task VerifyAsync_uniform_blank_image_returns_unreadable_status()
    {
        await using var provider = VerificationFixtureTests.BuildProviderForTests();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var service = provider.GetRequiredService<IVerificationService>();
        var blankPng = CreateUniformPng(900, 1200, 252);
        var expected = new ExpectedLabelFields
        {
            BrandName = "Test Brand",
            ClassTypeDesignation = "Bourbon",
            AbvPercent = 40.0m,
            NetContents = "750 mL",
            BottlerProducerAddress = "Test Distillery, Test City, TX",
            ProductCategory = "distilled_spirits",
            TtbWarningText = "GOVERNMENT WARNING: sample",
            BoldWarningPhrase = "GOVERNMENT WARNING:"
        };

        var result = await service.VerifyAsync(blankPng, expected);

        Assert.Equal(VerificationOutcome.Unreadable, result.OverallStatus);
        Assert.False(result.IsVerified);
        Assert.NotEmpty(result.Fields);
        Assert.NotNull(result.StatusMessage);
        Assert.NotNull(result.AgentGuidance);
    }

    [Fact]
    public async Task VerifyAsync_baseline_fixture_remains_readable()
    {
        await using var provider = VerificationFixtureTests.BuildProviderForTests();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var repoRoot = FindRepoRoot();
        var baselinePath = Path.Combine(repoRoot, "testdata", "fixtures", "baseline-01.png");
        if (!File.Exists(baselinePath))
        {
            return;
        }

        var expected = System.Text.Json.JsonSerializer.Deserialize<ExpectedLabelFields>(
            await File.ReadAllTextAsync(Path.Combine(repoRoot, "testdata", "fixtures", "baseline-01.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var service = provider.GetRequiredService<IVerificationService>();
        var result = await service.VerifyAsync(await File.ReadAllBytesAsync(baselinePath), expected);

        Assert.NotEqual(VerificationOutcome.Unreadable, result.OverallStatus);
    }

    private static IOcrReadabilityAssessor BuildAssessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IImagePreprocessor, OpenCvImagePreprocessor>();
        services.Configure<ReadabilityOptions>(_ => { });
        services.AddSingleton<IOcrReadabilityAssessor, OcrReadabilityAssessor>();
        return services.BuildServiceProvider().GetRequiredService<IOcrReadabilityAssessor>();
    }

    private static byte[] CreateUniformPng(int width, int height, byte gray)
    {
        using var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(gray));
        return mat.ToBytes(".png");
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
