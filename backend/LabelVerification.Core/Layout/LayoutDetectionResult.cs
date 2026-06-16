namespace LabelVerification.Core.Layout;

public sealed record LayoutDetectionResult(
    DocumentLayoutClass DocumentClass,
    IReadOnlyList<LabelFieldRegion> Regions,
    double OverallConfidence,
    string PrimarySource);
