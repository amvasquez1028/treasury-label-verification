using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Models;
using LabelVerification.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class VenenosaVerificationTests(OcrEngineFixture ocrFixture)
{
    [Fact]
    public async Task Venenosa_odp_passes_with_garbled_warning_ocr()
    {
        var planRoot = FindPlanRepoRoot();
        var manifestPath = Path.Combine(planRoot, "public", "samples", "manifest.json");
        var manifest = JsonSerializer.Deserialize<List<ManifestItem>>(
            await File.ReadAllTextAsync(manifestPath),
            JsonOptions) ?? [];
        var item = manifest.First(i => i.File == "04-odp-la-venenosa-raicilla.png");
        Assert.NotNull(item.ExpectedLabelFields);

        var expected = item.ExpectedLabelFields with { LabelPresentation = LabelPresentation.FullLabel };
        var bytes = await File.ReadAllBytesAsync(Path.Combine(planRoot, "public", "samples", item.File));
        var result = await ocrFixture.Provider.GetRequiredService<IVerificationService>().VerifyAsync(bytes, expected);

        var failed = result.Fields.Where(f => !f.IsMatch).Select(f => f.FieldName).ToList();
        Assert.True(
            result.OverallStatus == VerificationOutcome.Pass,
            $"Venenosa should pass; failed=[{string.Join(", ", failed)}]; ocr={result.RawOcrText[..Math.Min(400, result.RawOcrText.Length)]}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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

    private sealed record ManifestItem
    {
        public required string File { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }
}
