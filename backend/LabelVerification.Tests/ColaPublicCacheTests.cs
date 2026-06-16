using LabelVerification.Core.Cola;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Tests;

public class ColaPublicCacheTests
{
    [Fact]
    public void TryGetEntry_repairs_common_13_digit_ocr_ttb_id_for_jack()
    {
        using var provider = BuildColaProvider();
        var cache = provider.GetRequiredService<IColaPublicCache>();

        var ocrCorrupted = "1334301000271";
        Assert.True(cache.TryGetEntry(ocrCorrupted, out var entry));
        Assert.Contains("JACK", entry.Expected.BrandName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("13343001000271", cache.ListIds(), StringComparer.Ordinal);
    }

    [Fact]
    public void TryGetEntry_returns_exact_match_without_repair()
    {
        using var provider = BuildColaProvider();
        var cache = provider.GetRequiredService<IColaPublicCache>();

        Assert.True(cache.TryGetEntry("13343001000271", out _));
    }

    private static ServiceProvider BuildColaProvider()
    {
        var root = LabelVerifyWebFactory.FindPlanRepoRoot();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cola:ColasDir"] = Path.Combine(root, "testdata", "colas"),
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
    }
}
