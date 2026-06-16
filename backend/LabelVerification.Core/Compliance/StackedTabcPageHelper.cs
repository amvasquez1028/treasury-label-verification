using OpenCvSharp;

namespace LabelVerification.Core.Compliance;

internal static class StackedTabcPageHelper
{
    internal static IReadOnlyList<byte[]> Split(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        if (mat.Height / (double)Math.Max(1, mat.Width) <= 2.0)
        {
            return [imageBytes];
        }

        var aspect = mat.Height / (double)Math.Max(1, mat.Width);
        var estimatedPageHeight = mat.Width * (3300.0 / 2550.0);
        var pageCount = Math.Max(2, (int)Math.Ceiling(mat.Height / estimatedPageHeight));

        // Tall Texas ODP PDFs are cert + label + warning (3 pages). A 2-page split misaligns brand/warning ROIs.
        if (aspect >= 2.5 && mat.Height >= 6000 && pageCount < 3)
        {
            pageCount = 3;
        }

        var segmentHeight = (int)Math.Ceiling(mat.Height / (double)pageCount);
        var pages = new List<byte[]>();

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex += 1)
        {
            var top = pageIndex * segmentHeight;
            if (top >= mat.Height)
            {
                break;
            }

            var height = Math.Min(segmentHeight, mat.Height - top);
            using var roi = new Mat(mat, new Rect(0, top, mat.Width, height));
            pages.Add(roi.ToBytes(".png"));
        }

        return pages;
    }

    internal static bool IsSubmissionGradeFlatArtwork(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var longest = Math.Max(mat.Width, mat.Height);
        var shortest = Math.Min(mat.Width, mat.Height);
        return longest >= 2000 && shortest >= 900;
    }

    /// <summary>
    /// Stacked TABC PDFs place certificate pages first; product label art is on the last page (2-up)
    /// or the middle page (3+). TTB warning footer is captured via composite-bottom OCR on the full stack.
    /// </summary>
    internal static IReadOnlyList<byte[]> SelectDeepOcrPages(
        IReadOnlyList<byte[]> pages,
        bool includeCertificatePage = false)
    {
        if (pages.Count <= 2)
        {
            return pages;
        }

        if (!includeCertificatePage)
        {
            return pages.Skip(pages.Count - 2).ToArray();
        }

        return pages.Skip(pages.Count - 2).Concat(pages.Take(1)).ToArray();
    }

    internal static bool TryGetCertificatePage(IReadOnlyList<byte[]> pages, out byte[] certificatePage) =>
        TryGetPageAt(pages, 0, out certificatePage);

    internal static bool TryGetPageAt(IReadOnlyList<byte[]> pages, int index, out byte[] pageBytes)
    {
        if (index >= 0 && index < pages.Count)
        {
            pageBytes = pages[index];
            return true;
        }

        pageBytes = Array.Empty<byte>();
        return false;
    }
}
