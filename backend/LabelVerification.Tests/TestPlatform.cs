using System.Runtime.InteropServices;

namespace LabelVerification.Tests;

internal static class TestPlatform
{
    internal static bool IsWindowsDev =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static bool SupportsFullOdpStackOcr =>
        !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
