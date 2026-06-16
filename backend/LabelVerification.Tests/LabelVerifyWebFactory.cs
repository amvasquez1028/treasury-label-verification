using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LabelVerification.Tests;

public sealed class LabelVerifyWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Auth", "Data Source=test-verify-auth.db");
        builder.UseSetting("SEED_DEMO_USERS", "true");
        builder.UseSetting("DEMO_AGENT_PASSWORD", TestCredentials.DemoAgentPassword);
        builder.UseSetting("DISABLE_PUBLIC_REGISTRATION", "true");
        builder.UseSetting("Ocr:TessDataPath", FindTessDataPath());
        builder.UseSetting("Ocr:TimeoutSeconds", "25");
        builder.UseSetting("Ocr:PerLabelWallClockMs", "15000");
        builder.UseSetting("Ocr:SubmissionGradeSupplementWallClockMs", "6000");
        builder.UseSetting("Ocr:UseFieldBandTargetedOcr", "true");
        builder.UseSetting("Ocr:FlatArtworkMaxOcrSide", "1200");
        builder.UseSetting("Cola:ColasDir", FindColasDir());
    }

    internal static string FindPlanRepoRoot()
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

    private static string FindTessDataPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tessdata");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return "tessdata";
    }

    private static string FindColasDir()
    {
        var root = FindPlanRepoRoot();
        return Path.Combine(root, "testdata", "colas");
    }
}
