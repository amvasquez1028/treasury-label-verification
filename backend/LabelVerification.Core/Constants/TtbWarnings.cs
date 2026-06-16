namespace LabelVerification.Core.Constants;

public static class TtbWarnings
{
    public const string StandardGovernmentWarning =
        "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

    public static readonly IReadOnlyList<string> KnownVariants = new[]
    {
        StandardGovernmentWarning,
        "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems"
    };
}
