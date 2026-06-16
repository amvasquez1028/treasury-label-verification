namespace LabelVerification.Core.Extraction;

public sealed record ExtractedLabelFields
{
    public string? TtbId { get; init; }
    public string? BrandName { get; init; }
    public string? FancifulName { get; init; }
    public string? ClassTypeDesignation { get; init; }
    public decimal? AbvPercent { get; init; }
    public string? NetContents { get; init; }
    public string? BottlerProducerAddress { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? ProductCategory { get; init; }
    public string? TtbWarningText { get; init; }
    public string? BoldWarningPhrase { get; init; }
    public string? BarcodeUpc { get; init; }
}

public sealed record FieldExtractionConfidence
{
    public required string FieldName { get; init; }
    public required double Confidence { get; init; }
    public string? Source { get; init; }
}

public sealed record LabelExtractionResult
{
    public required ExtractedLabelFields Fields { get; init; }
    public required IReadOnlyList<FieldExtractionConfidence> Confidences { get; init; }
    public required string RawOcrText { get; init; }
    public required IReadOnlyDictionary<string, string> RegionTexts { get; init; }
    public required Layout.LayoutDetectionResult Layout { get; init; }
    public required long ProcessingTimeMs { get; init; }
}

public sealed record AutonomousVerificationResult
{
    public required Models.VerificationResult Verification { get; init; }
    public required LabelExtractionResult Extraction { get; init; }
    public string? ResolvedTtbId { get; init; }
    public bool ColaRegistryHit { get; init; }
    public string? AgentGuidance { get; init; }
}
