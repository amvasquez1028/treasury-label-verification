namespace LabelVerification.Core.Options;

public sealed class LayoutOptions
{
    public const string SectionName = "Layout";

    /// <summary>Directory containing layout-annotations/annotations.json (US-labeled training set).</summary>
    public string AnnotationsDir { get; set; } = "testdata/layout-annotations";

    /// <summary>Optional ONNX layout model (exported via scripts/export-layout-onnx.py).</summary>
    public string? ModelPath { get; set; } = "testdata/layout-models/label-layout-v1.onnx";

    /// <summary>Use ONNX when model file exists; otherwise annotation anchors + heuristics.</summary>
    public bool PreferOnnx { get; set; } = true;

    /// <summary>Minimum IoU-weighted confidence to prefer layout ROIs over full-page OCR.</summary>
    public double MinRegionConfidence { get; set; } = 0.55;

    /// <summary>Ms budget for layout-guided ROI OCR (within overall SubmissionGradeTargetMs).</summary>
    public int RoiOcrBudgetMs { get; set; } = 1600;

    /// <summary>When false, extraction uses submission-grade baseline OCR only (faster on constrained hosts).</summary>
    public bool EnableGuidedRoiOcr { get; set; } = false;
}
