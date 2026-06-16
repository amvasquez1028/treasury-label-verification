namespace LabelVerification.Core.Models;

public enum VerificationOutcome
{
    Pass,
    Fail,
    Review,
    Timeout,
    Unreadable
}

public enum LabelPresentation
{
    FullLabel,
    BottleFront,
    RealBottleFrontWithWarningCheck
}

public sealed record ExpectedLabelFields
{
    public required string BrandName { get; init; }
    public string? FancifulName { get; init; }
    public required string ClassTypeDesignation { get; init; }
    public required decimal AbvPercent { get; init; }
    public required string NetContents { get; init; }
    public required string BottlerProducerAddress { get; init; }
    public string? CountryOfOrigin { get; init; }
    public required string ProductCategory { get; init; }
    public required string TtbWarningText { get; init; }
    public required string BoldWarningPhrase { get; init; }
    public LabelPresentation LabelPresentation { get; init; } = LabelPresentation.FullLabel;
    public string? Appellation { get; init; }
    public string? Vintage { get; init; }
    public string? SulfiteDeclaration { get; init; }
    public string? OrganicClaim { get; init; }
    public string? BarcodeUpc { get; init; }
}

public sealed record FieldVerificationResult
{
    public required string FieldName { get; init; }
    public required bool IsMatch { get; init; }
    public required double Confidence { get; init; }
    public string? ExtractedValue { get; init; }
    public string? ExpectedValue { get; init; }
    public string? Notes { get; init; }
}

public sealed record VerificationResult
{
    public required VerificationOutcome OverallStatus { get; init; }
    public required bool IsVerified { get; init; }
    public required double OverallConfidence { get; init; }
    public required IReadOnlyList<FieldVerificationResult> Fields { get; init; }
    public required string RawOcrText { get; init; }
    public required long ProcessingTimeMs { get; init; }
    public string? StatusMessage { get; init; }
    public string? AgentGuidance { get; init; }
}

public sealed record BatchVerificationItemResult
{
    public required string FileName { get; init; }
    public VerificationResult? Result { get; init; }
    public string? Error { get; init; }
}

public sealed record BatchVerificationResult
{
    public required IReadOnlyList<BatchVerificationItemResult> Items { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
}
