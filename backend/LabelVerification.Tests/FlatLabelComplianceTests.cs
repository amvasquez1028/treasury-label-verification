using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Compliance;
using LabelVerification.Core.Constants;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class FlatLabelComplianceTests(OcrEngineFixture ocrFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task Odp_flat_samples_include_compliance_checks()
    {
        var planRoot = FindPlanRepoRoot();
        var manifestPath = Path.Combine(planRoot, "public", "samples", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var provider = ocrFixture.Provider;
        var service = provider.GetRequiredService<IVerificationService>();

        var samplesDir = Path.Combine(planRoot, "public", "samples");
        var manifest = JsonSerializer.Deserialize<List<SampleItem>>(
            await File.ReadAllTextAsync(manifestPath),
            JsonOptions) ?? [];

        foreach (var item in manifest.Where(i => i.SampleKind == "odp_approved_label"))
        {
            if (TestPlatform.IsWindowsDev && item.File is not "05-odp-jack-daniels-old-no7.png")
            {
                continue;
            }

            var imagePath = Path.Combine(samplesDir, item.File);
            if (!File.Exists(imagePath) || item.ExpectedLabelFields is null)
            {
                continue;
            }

            var expected = item.ExpectedLabelFields with { LabelPresentation = LabelPresentation.FullLabel };
            var result = await service.VerifyAsync(await File.ReadAllBytesAsync(imagePath), expected);
            var complianceFields = result.Fields
                .Where(field => field.FieldName is "WarningPlacement" or "WarningContrast" or "BoldWarningTypography" or "LabelTextContrast")
                .Select(field => field.FieldName)
                .ToList();

            Assert.Equal(4, complianceFields.Count);
            Assert.Equal(VerificationOutcome.Pass, result.OverallStatus);
        }
    }

    [Fact]
    public void Flat_compliance_analyzer_applies_only_to_full_label_flat_artwork()
    {
        using var provider = OcrEngineFixture.BuildProvider();
        var analyzer = provider.GetRequiredService<IFlatLabelComplianceAnalyzer>();
        var synthetic = CreateSyntheticFlatLabelPng();
        var expected = new ExpectedLabelFields
        {
            BrandName = "TEST BRAND",
            ClassTypeDesignation = "WHISKEY",
            AbvPercent = 40,
            NetContents = "750 ML",
            BottlerProducerAddress = "TEST DISTILLERY",
            ProductCategory = "distilled_spirits",
            TtbWarningText = TtbWarnings.StandardGovernmentWarning,
            BoldWarningPhrase = "GOVERNMENT WARNING:",
            LabelPresentation = LabelPresentation.FullLabel,
        };

        Assert.True(analyzer.AppliesTo(synthetic, expected));
        Assert.False(analyzer.AppliesTo(synthetic, expected with { LabelPresentation = LabelPresentation.BottleFront }));
    }

    private static byte[] CreateSyntheticFlatLabelPng()
    {
        using var mat = new OpenCvSharp.Mat(2700, 2100, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.White);
        OpenCvSharp.Cv2.PutText(mat, "TEST BRAND", new OpenCvSharp.Point(80, 180), OpenCvSharp.HersheyFonts.HersheySimplex, 1.4, OpenCvSharp.Scalar.Black, 3);
        OpenCvSharp.Cv2.PutText(mat, "750 ML", new OpenCvSharp.Point(80, 260), OpenCvSharp.HersheyFonts.HersheySimplex, 1.0, OpenCvSharp.Scalar.Black, 2);
        OpenCvSharp.Cv2.PutText(mat, "GOVERNMENT WARNING:", new OpenCvSharp.Point(60, 980), OpenCvSharp.HersheyFonts.HersheySimplex, 1.1, OpenCvSharp.Scalar.Black, 4);
        OpenCvSharp.Cv2.PutText(mat, "SURGEON GENERAL", new OpenCvSharp.Point(60, 1040), OpenCvSharp.HersheyFonts.HersheySimplex, 0.7, OpenCvSharp.Scalar.Black, 2);
        OpenCvSharp.Cv2.PutText(mat, "PREGNANCY", new OpenCvSharp.Point(60, 1090), OpenCvSharp.HersheyFonts.HersheySimplex, 0.7, OpenCvSharp.Scalar.Black, 2);
        return mat.ToBytes(".png");
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

        throw new DirectoryNotFoundException("Could not locate plan repo root.");
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

    private sealed record SampleItem
    {
        public required string File { get; init; }
        public string? SampleKind { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
