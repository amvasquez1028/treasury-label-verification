namespace LabelVerification.Core.Options;

public sealed class OcrOptions
{
    public const string SectionName = "Ocr";
    public string TessDataPath { get; set; } = "tessdata";
    /// <summary>Hard ceiling for a single verify request (ms budget + matcher/compliance overhead).</summary>
    public int TimeoutSeconds { get; set; } = 3;
    public int MaxParallel { get; set; } = 4;
    /// <summary>Longest side for submission-grade flat label OCR (px).</summary>
    public int FlatArtworkMaxOcrSide { get; set; } = 1800;
    /// <summary>Parallel Tesseract engines for stacked flat-label page OCR.</summary>
    public int FlatArtworkEnginePoolSize { get; set; } = 4;
    /// <summary>Wall-clock ms budget for OCR passes per label (P1v3 batch target: 1800).</summary>
    public int SubmissionGradeTargetMs { get; set; } = 2000;
    /// <summary>Hard wall-clock ms cap for all OCR work on one label (frontend+backend target: 4500).</summary>
    public int PerLabelWallClockMs { get; set; } = 8000;
    /// <summary>Additional wall-clock ms for cert/warning supplement OCR after the main pass (submission-grade stacks).</summary>
    public int SubmissionGradeSupplementWallClockMs { get; set; } = 6000;
    /// <summary>Use OpenCV ink-projection bands to target footer/contents/header ROIs on 3+ page stacks.</summary>
    public bool UseFieldBandTargetedOcr { get; set; }
}

public sealed class VerificationOptions
{
    public const string SectionName = "Verification";
    public decimal AbvTolerance { get; set; } = 0.1m;
    public double BrandMatchThreshold { get; set; } = 0.75;
}

public sealed class ReadabilityOptions
{
    public const string SectionName = "Readability";
    public int MinAlphanumericCharacters { get; set; } = 25;
    public int MinSubstantiveWords { get; set; } = 4;
    public double MinBlurVariance { get; set; } = 40.0;
    public double MinContrastStdDev { get; set; } = 12.0;
    public int FlatArtworkMinAlphanumericCharacters { get; set; } = 40;
    public int FlatArtworkMinSubstantiveWords { get; set; } = 8;
    public double FlatArtworkMinBlurVariance { get; set; } = 8.0;
}

public sealed class FlatLabelComplianceOptions
{
    public const string SectionName = "FlatLabelCompliance";
    public double MinWarningContrastRatio { get; set; } = 3.5;
    public double MinLabelContrastRatio { get; set; } = 3.0;
    public double MinBoldTypographyConfidence { get; set; } = 0.55;
    public double MinFooterInkDensity { get; set; } = 0.04;
    public double WarningPlacementBottomMargin { get; set; } = 0.01;
}
