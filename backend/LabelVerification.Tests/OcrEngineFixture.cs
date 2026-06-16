using System.Runtime.InteropServices;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Tests;

/// <summary>
/// Primes Tesseract with a full submission-grade OCR pass so the first theory case is not cold-start garbage.
/// </summary>
public sealed class OcrEngineFixture : IDisposable
{
    public ServiceProvider Provider { get; }

    public OcrEngineFixture()
    {
        Provider = BuildProvider();
        var engine = Provider.GetRequiredService<ITesseractEngineProvider>();
        engine.WarmUp();
        PrimeWithJackOdp();
    }

    public void Dispose() => Provider.Dispose();

    private void PrimeWithJackOdp()
    {
        var planRoot = FindPlanRepoRoot();
        var jackPath = Path.Combine(planRoot, "public", "samples", "05-odp-jack-daniels-old-no7.png");
        if (!File.Exists(jackPath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(jackPath);
        _ = Provider.GetRequiredService<IOcrService>().ExtractTextAsync(bytes, default).GetAwaiter().GetResult();
    }

    internal static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var repoRoot = FindTessDataRoot();
        var enginePoolSize = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "1" : "6";
        var settings = new Dictionary<string, string?>
        {
            ["Ocr:TessDataPath"] = Path.Combine(repoRoot, "tessdata"),
            ["Ocr:TimeoutSeconds"] = "30",
            ["Ocr:FlatArtworkMaxOcrSide"] = "1200",
            ["Ocr:FlatArtworkEnginePoolSize"] = enginePoolSize,
            ["Ocr:SubmissionGradeTargetMs"] = "3500",
            ["Ocr:PerLabelWallClockMs"] = "15000",
            ["Ocr:SubmissionGradeSupplementWallClockMs"] = "6000",
            ["Ocr:UseFieldBandTargetedOcr"] = "true",
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                settings[key] = value;
            }
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .AddLabelVerificationCore(config)
            .BuildServiceProvider();
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
}
