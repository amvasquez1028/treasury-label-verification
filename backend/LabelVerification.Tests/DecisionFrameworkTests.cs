using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Tests;

public class DecisionFrameworkTests
{
    [Fact]
    public void ComputeOverallStatus_returns_pass_when_all_match_and_confidence_at_or_above_90()
    {
        var fields = new List<FieldVerificationResult>
        {
            Field("BrandName", true, 0.98),
            Field("Abv", true, 0.92)
        };

        var status = VerificationDecision.ComputeOverallStatus(fields);

        Assert.Equal(VerificationOutcome.Pass, status);
    }

    [Fact]
    public void ComputeOverallStatus_returns_pass_when_all_fields_match_even_if_confidence_below_90()
    {
        var fields = new List<FieldVerificationResult>
        {
            Field("BrandName", true, 0.98),
            Field("Abv", true, 0.85)
        };

        var status = VerificationDecision.ComputeOverallStatus(fields);

        Assert.Equal(VerificationOutcome.Pass, status);
    }

    [Fact]
    public void ComputeOverallStatus_returns_fail_when_any_field_mismatches()
    {
        var fields = new List<FieldVerificationResult>
        {
            Field("BrandName", true, 0.98),
            Field("TtbWarning", false, 0.88)
        };

        var status = VerificationDecision.ComputeOverallStatus(fields);

        Assert.Equal(VerificationOutcome.Fail, status);
    }

  [Fact]
    public async Task VerifyBatchAsync_pairs_each_image_with_matching_expected_fields()
    {
        await using var provider = VerificationFixtureTests.BuildProviderForTests();
        provider.GetRequiredService<ITesseractEngineProvider>().WarmUp();

        var fixturesDir = Path.Combine(FindRepoRoot(), "testdata", "fixtures");
        var baselinePath = Path.Combine(fixturesDir, "baseline-01.png");
        var variantPath = Path.Combine(fixturesDir, "variant-04.png");

        if (!File.Exists(baselinePath) || !File.Exists(variantPath))
        {
            return;
        }

        var baselineExpected = System.Text.Json.JsonSerializer.Deserialize<ExpectedLabelFields>(
            await File.ReadAllTextAsync(Path.Combine(fixturesDir, "baseline-01.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var variantExpected = System.Text.Json.JsonSerializer.Deserialize<ExpectedLabelFields>(
            await File.ReadAllTextAsync(Path.Combine(fixturesDir, "variant-04.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var service = provider.GetRequiredService<IVerificationService>();
        var batch = await service.VerifyBatchAsync([
            new BatchVerificationRequestItem("baseline-01.png", await File.ReadAllBytesAsync(baselinePath), baselineExpected),
            new BatchVerificationRequestItem("variant-04.png", await File.ReadAllBytesAsync(variantPath), variantExpected)
        ]);

        Assert.Equal(2, batch.Items.Count);
        Assert.All(batch.Items, item => Assert.NotNull(item.Result));
        Assert.Equal(baselineExpected.BrandName, batch.Items[0].Result!.Fields.First(f => f.FieldName == "BrandName").ExpectedValue);
        Assert.Equal(variantExpected.BrandName, batch.Items[1].Result!.Fields.First(f => f.FieldName == "BrandName").ExpectedValue);
    }

    private static FieldVerificationResult Field(string name, bool isMatch, double confidence) =>
        new()
        {
            FieldName = name,
            IsMatch = isMatch,
            Confidence = confidence,
            ExpectedValue = "expected",
            ExtractedValue = isMatch ? "expected" : "other",
            Notes = null
        };

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
