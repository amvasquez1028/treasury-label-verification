using LabelVerification.Core.Compliance;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace LabelVerification.Core.Layout;

internal sealed class AnnotationAnchorLayoutDetector
{
    private readonly ILayoutAnnotationStore _store;
    private readonly IImagePreprocessor _preprocessor;

    public AnnotationAnchorLayoutDetector(ILayoutAnnotationStore store, IImagePreprocessor preprocessor)
    {
        _store = store;
        _preprocessor = preprocessor;
    }

    public LayoutDetectionResult Detect(byte[] imageBytes, DocumentLayoutClass documentClass)
    {
        if (!_store.AnchorTemplates.TryGetValue(documentClass, out var anchors) || anchors.Count == 0)
        {
            return HeuristicLayoutFallback.Detect(imageBytes, documentClass, _preprocessor);
        }

        var pages = documentClass is DocumentLayoutClass.OdpStack3Page or DocumentLayoutClass.OdpStack2Page
            ? StackedTabcPageHelper.Split(imageBytes)
            : (IReadOnlyList<byte[]>)[imageBytes];

        var regions = new List<LabelFieldRegion>();
        foreach (var anchor in anchors)
        {
            var pageIndex = Math.Clamp(anchor.PageIndex, 0, Math.Max(0, pages.Count - 1));
            var pageBytes = pages[pageIndex];
            using var pageMat = Mat.FromImageData(pageBytes, ImreadModes.Color);
            regions.Add(new LabelFieldRegion(
                anchor.Field,
                anchor.X,
                anchor.Y,
                anchor.Width,
                anchor.Height,
                pageIndex,
                Confidence: 0.82,
                Source: "annotation-anchor"));
        }

        return new LayoutDetectionResult(documentClass, regions, 0.82, "annotation-anchor");
    }
}

internal static class HeuristicLayoutFallback
{
    internal static LayoutDetectionResult Detect(
        byte[] imageBytes,
        DocumentLayoutClass documentClass,
        IImagePreprocessor preprocessor)
    {
        var regions = new List<LabelFieldRegion>();

        switch (documentClass)
        {
            case DocumentLayoutClass.OdpStack3Page:
            {
                var pages = StackedTabcPageHelper.Split(imageBytes);
                AddPageBand(regions, LayoutFieldKind.CertificateHeader, 0, 0.0, 0.0, 1.0, 0.14);
                if (pages.Count >= 2)
                {
                    AddPageBand(regions, LayoutFieldKind.BrandBlock, 1, 0.1, 0.05, 0.8, 0.35);
                    AddPageBand(regions, LayoutFieldKind.ClassType, 1, 0.1, 0.35, 0.8, 0.2);
                    AddPageBand(regions, LayoutFieldKind.BottlerAddress, 1, 0.1, 0.5, 0.8, 0.2);
                }

                if (pages.Count >= 3)
                {
                    AddPageBand(regions, LayoutFieldKind.GovernmentWarning, 2, 0.08, 0.55, 0.84, 0.4);
                }

                break;
            }

            case DocumentLayoutClass.OdpStack2Page:
            {
                AddPageBand(regions, LayoutFieldKind.CertificateHeader, 0, 0.0, 0.0, 1.0, 0.16);
                AddPageBand(regions, LayoutFieldKind.BrandBlock, 1, 0.08, 0.08, 0.84, 0.28);
                AddPageBand(regions, LayoutFieldKind.ClassType, 1, 0.08, 0.3, 0.84, 0.15);
                AddPageBand(regions, LayoutFieldKind.BottlerAddress, 1, 0.08, 0.55, 0.84, 0.2);
                AddPageBand(regions, LayoutFieldKind.GovernmentWarning, 1, 0.06, 0.72, 0.88, 0.25);
                break;
            }

            case DocumentLayoutClass.BottlePhoto:
            {
                AddPageBand(regions, LayoutFieldKind.BrandBlock, 0, 0.15, 0.18, 0.7, 0.28);
                AddPageBand(regions, LayoutFieldKind.ClassType, 0, 0.15, 0.38, 0.7, 0.12);
                AddPageBand(regions, LayoutFieldKind.GovernmentWarning, 0, 0.1, 0.68, 0.8, 0.25);
                break;
            }

            default:
            {
                AddPageBand(regions, LayoutFieldKind.BrandBlock, 0, 0.08, 0.08, 0.84, 0.35);
                AddPageBand(regions, LayoutFieldKind.ClassType, 0, 0.08, 0.35, 0.84, 0.2);
                AddPageBand(regions, LayoutFieldKind.GovernmentWarning, 0, 0.06, 0.62, 0.88, 0.3);
                break;
            }
        }

        _ = preprocessor;
        return new LayoutDetectionResult(documentClass, regions, 0.65, "heuristic-fallback");
    }

    private static void AddPageBand(
        List<LabelFieldRegion> regions,
        LayoutFieldKind field,
        int pageIndex,
        double x,
        double y,
        double width,
        double height)
    {
        regions.Add(new LabelFieldRegion(field, x, y, width, height, pageIndex, 0.65, "heuristic-fallback"));
    }
}
