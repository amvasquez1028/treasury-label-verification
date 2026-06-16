using LabelVerification.Core.Models;

namespace LabelVerification.Core.Services;

public static class VerificationDecision
{
    public const double PassConfidenceThreshold = 0.90;
    public const double ReviewBandLower = 0.65;

    public static VerificationOutcome ComputeOverallStatus(IReadOnlyList<FieldVerificationResult> fields)
    {
        if (fields.Count == 0)
        {
            return VerificationOutcome.Review;
        }

        var failedFields = fields.Where(f => !f.IsMatch).ToList();
        if (failedFields.Count > 0)
        {
            var nearMiss = failedFields.All(f =>
                f.Confidence >= ReviewBandLower && f.Confidence < GetMatchThreshold(f.FieldName));

            if (nearMiss)
            {
                return VerificationOutcome.Review;
            }

            return VerificationOutcome.Fail;
        }

        return VerificationOutcome.Pass;
    }

    private static double GetMatchThreshold(string fieldName) =>
        fieldName is "TtbWarningText" or "BoldWarningPhrase" ? 1.0 : 0.75;
}
