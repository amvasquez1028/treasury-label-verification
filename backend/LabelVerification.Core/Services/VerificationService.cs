using LabelVerification.Core.Compliance;
using LabelVerification.Core.Cola;
using LabelVerification.Core.Extraction;
using LabelVerification.Core.Layout;
using LabelVerification.Core.Matching;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Services;

public interface IVerificationService
{
    Task<VerificationResult> VerifyAsync(
        byte[] imageBytes,
        ExpectedLabelFields expected,
        string? preExtractedOcrText = null,
        bool useClientOcr = false,
        bool skipServerOcrWhenClientTextPresent = false,
        CancellationToken cancellationToken = default);

    Task<BatchVerificationResult> VerifyBatchAsync(
        IReadOnlyList<BatchVerificationRequestItem> items,
        CancellationToken cancellationToken = default);
}

public sealed record BatchVerificationRequestItem(
    string FileName,
    byte[] Bytes,
    ExpectedLabelFields Expected,
    string? OcrText = null,
    bool UseClientOcr = false);

public sealed class VerificationService : IVerificationService
{
    private readonly IOcrService _ocrService;
    private readonly IOcrReadabilityAssessor _readabilityAssessor;
    private readonly IFlatLabelComplianceAnalyzer _flatLabelComplianceAnalyzer;
    private readonly IEnumerable<IFieldMatcher> _matchers;
    private readonly OcrOptions _ocrOptions;
    private readonly VerificationOptions _verificationOptions;
    private readonly FlatLabelComplianceOptions _flatLabelComplianceOptions;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        IOcrService ocrService,
        IOcrReadabilityAssessor readabilityAssessor,
        IFlatLabelComplianceAnalyzer flatLabelComplianceAnalyzer,
        IEnumerable<IFieldMatcher> matchers,
        IOptions<OcrOptions> ocrOptions,
        IOptions<VerificationOptions> verificationOptions,
        IOptions<FlatLabelComplianceOptions> flatLabelComplianceOptions,
        ILogger<VerificationService> logger)
    {
        _ocrService = ocrService;
        _readabilityAssessor = readabilityAssessor;
        _flatLabelComplianceAnalyzer = flatLabelComplianceAnalyzer;
        _matchers = matchers;
        _ocrOptions = ocrOptions.Value;
        _verificationOptions = verificationOptions.Value;
        _flatLabelComplianceOptions = flatLabelComplianceOptions.Value;
        _logger = logger;
    }

    public async Task<VerificationResult> VerifyAsync(
        byte[] imageBytes,
        ExpectedLabelFields expected,
        string? preExtractedOcrText = null,
        bool useClientOcr = false,
        bool skipServerOcrWhenClientTextPresent = false,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_ocrOptions.TimeoutSeconds));
        var started = Environment.TickCount64;

        try
        {
            var clientText = preExtractedOcrText?.Trim() ?? string.Empty;
            var ocrKindHint = LabelPresentationRules.UsesRelaxedFrontPhotoThreshold(expected.LabelPresentation)
                ? LabelImageKind.BottlePhoto
                : (LabelImageKind?)null;
            var submissionGradeSupplement = string.Empty;
            if (!skipServerOcrWhenClientTextPresent
                && _flatLabelComplianceAnalyzer.AppliesTo(imageBytes, expected))
            {
                submissionGradeSupplement = await _ocrService.ExtractSubmissionGradeSupplementAsync(
                    imageBytes,
                    string.Empty,
                    timeoutCts.Token);
            }

            var serverText = skipServerOcrWhenClientTextPresent && clientText.Length > 0
                ? string.Empty
                : await _ocrService.ExtractTextAsync(imageBytes, timeoutCts.Token, ocrKindHint);
            var ocrText = clientText.Length > 0
                ? MergeOcrText(clientText, MergeOcrText(submissionGradeSupplement, serverText))
                : MergeOcrText(submissionGradeSupplement, serverText);

            var boldConfidence = FlatArtworkOcrSufficiency.HasWarningCorpus(ocrText)
                || _flatLabelComplianceAnalyzer.AppliesTo(imageBytes, expected)
                ? 1.0
                : skipServerOcrWhenClientTextPresent
                    ? 0.85
                    : await _ocrService.GetBoldConfidenceAsync(
                        imageBytes,
                        expected.BoldWarningPhrase,
                        timeoutCts.Token);
            var fields = BuildFieldResults(
                imageBytes,
                expected,
                ocrText,
                boldConfidence,
                skipFlatLabelCompliance: skipServerOcrWhenClientTextPresent && clientText.Length > 0);
            var readability = skipServerOcrWhenClientTextPresent && clientText.Length > 0
                ? new OcrReadabilityAssessment(true, null, null)
                : _readabilityAssessor.Assess(imageBytes, ocrText);

            if (!readability.IsReadable)
            {
                _logger.LogInformation("Label unreadable: {Reason}", readability.Reason);

                return new VerificationResult
                {
                    OverallStatus = VerificationOutcome.Unreadable,
                    IsVerified = false,
                    OverallConfidence = fields.Count == 0 ? 0 : fields.Min(f => f.Confidence),
                    Fields = fields,
                    RawOcrText = ocrText,
                    ProcessingTimeMs = Environment.TickCount64 - started,
                    StatusMessage = readability.Reason,
                    AgentGuidance = readability.AgentGuidance
                };
            }

            var overallConfidence = fields.Count == 0 ? 0 : fields.Min(f => f.Confidence);
            var overallStatus = VerificationDecision.ComputeOverallStatus(fields);

            return new VerificationResult
            {
                OverallStatus = overallStatus,
                IsVerified = overallStatus == VerificationOutcome.Pass,
                OverallConfidence = Math.Round(overallConfidence, 4),
                Fields = fields,
                RawOcrText = ocrText,
                ProcessingTimeMs = Environment.TickCount64 - started
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Verification timed out after {Seconds}s", _ocrOptions.TimeoutSeconds);
            return BuildTimeoutResult(started);
        }
    }

    private VerificationResult BuildTimeoutResult(long startedTicks) =>
        new()
        {
            OverallStatus = VerificationOutcome.Timeout,
            IsVerified = false,
            OverallConfidence = 0,
            Fields = Array.Empty<FieldVerificationResult>(),
            RawOcrText = string.Empty,
            ProcessingTimeMs = Environment.TickCount64 - startedTicks,
            StatusMessage = "Processing exceeded the per-label time limit.",
            AgentGuidance = "Retry with a clearer image or fewer labels in the batch.",
        };

    private List<FieldVerificationResult> BuildFieldResults(
        byte[] imageBytes,
        ExpectedLabelFields expected,
        string ocrText,
        double boldConfidence,
        bool skipFlatLabelCompliance = false)
    {
        var options = _verificationOptions;

        var fields = _matchers
            .Where(m => ShouldEvaluateField(m.FieldName, expected))
            .Select(m => m.Match(ocrText, expected, options))
            .ToList();
        var boldField = fields.FirstOrDefault(f => f.FieldName == "BoldWarningPhrase");
        var warningField = fields.FirstOrDefault(f => f.FieldName == "TtbWarningText");
        if (boldField is not null)
        {
            var idx = fields.FindIndex(f => f.FieldName == "BoldWarningPhrase");
            var mergedConfidence = boldField.IsMatch
                ? Math.Max(boldField.Confidence, boldConfidence)
                : Math.Min(boldField.Confidence, boldConfidence);
            var requiresBoldHeuristic = expected.LabelPresentation != LabelPresentation.BottleFront;
            var warningConfirmed = warningField?.IsMatch == true;
            var isMatch = (boldField.IsMatch || warningConfirmed)
                && (!requiresBoldHeuristic || mergedConfidence >= 0.55 || warningConfirmed);
            fields[idx] = boldField with
            {
                Confidence = Math.Round(warningConfirmed || mergedConfidence >= 0.55 ? Math.Max(mergedConfidence, 1.0) : mergedConfidence, 4),
                IsMatch = isMatch,
                Notes = isMatch ? boldField.Notes : "Bold warning phrase not detected"
            };
        }

        if (!skipFlatLabelCompliance && _flatLabelComplianceAnalyzer.AppliesTo(imageBytes, expected))
        {
            fields.AddRange(_flatLabelComplianceAnalyzer.Analyze(imageBytes, expected, ocrText));
        }

        ApplyVisualWarningReconciliation(imageBytes, ocrText, fields);

        return fields;
    }

    private void ApplyVisualWarningReconciliation(
        byte[] imageBytes,
        string ocrText,
        List<FieldVerificationResult> fields)
    {
        if (!FlatWarningVisualConfirmation.TryConfirmFromFlatArtwork(
                imageBytes,
                ocrText,
                _flatLabelComplianceOptions,
                out var rationale))
        {
            ApplyWarningConfidenceCommentary(fields);
            return;
        }

        UpgradeWarningField(fields, "TtbWarningText", expectedValue =>
            new FieldVerificationResult
            {
                FieldName = "TtbWarningText",
                IsMatch = true,
                Confidence = 0.82,
                ExpectedValue = expectedValue,
                ExtractedValue = "Confirmed via Texas ODP warning-page layout (OCR partially garbled)",
                Notes = rationale,
            });

        UpgradeWarningField(fields, "BoldWarningPhrase", expectedValue =>
            new FieldVerificationResult
            {
                FieldName = "BoldWarningPhrase",
                IsMatch = true,
                Confidence = 0.82,
                ExpectedValue = expectedValue,
                ExtractedValue = "GOVERNMENT WARNING:",
                Notes = rationale,
            });

        ApplyWarningConfidenceCommentary(fields);
    }

    private static void UpgradeWarningField(
        List<FieldVerificationResult> fields,
        string fieldName,
        Func<string?, FieldVerificationResult> buildPass)
    {
        var idx = fields.FindIndex(f => f.FieldName == fieldName);
        if (idx < 0)
        {
            return;
        }

        var current = fields[idx];
        if (current.IsMatch)
        {
            return;
        }

        fields[idx] = buildPass(current.ExpectedValue);
    }

    private static void ApplyWarningConfidenceCommentary(List<FieldVerificationResult> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field.IsMatch || !IsWarningRelatedField(field.FieldName))
            {
                continue;
            }

            fields[i] = field with
            {
                Notes = FieldConfidenceCommentary.ForWarningField(
                    field.FieldName,
                    false,
                    field.Confidence,
                    field.Notes),
            };
        }
    }

    private static bool IsWarningRelatedField(string fieldName) =>
        fieldName is "TtbWarningText"
            or "BoldWarningPhrase"
            or "WarningPlacement"
            or "BoldWarningTypography"
            or "WarningContrast";

    private static bool ShouldEvaluateField(string fieldName, ExpectedLabelFields expected)
    {
        if (expected.LabelPresentation == LabelPresentation.BottleFront)
        {
            return fieldName is not "TtbWarningText" and not "BoldWarningPhrase";
        }

        return fieldName switch
        {
            "Appellation" => !string.IsNullOrWhiteSpace(expected.Appellation),
            "Vintage" => !string.IsNullOrWhiteSpace(expected.Vintage),
            "SulfiteDeclaration" => !string.IsNullOrWhiteSpace(expected.SulfiteDeclaration),
            "OrganicClaim" => !string.IsNullOrWhiteSpace(expected.OrganicClaim),
            "BarcodeUpc" => !string.IsNullOrWhiteSpace(expected.BarcodeUpc),
            _ => true,
        };
    }

    private static string MergeOcrText(string clientText, string serverText)
    {
        if (string.IsNullOrWhiteSpace(clientText))
        {
            return serverText?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(serverText))
        {
            return clientText.Trim();
        }

        var lines = clientText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(serverText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", lines);
    }

    public async Task<BatchVerificationResult> VerifyBatchAsync(
        IReadOnlyList<BatchVerificationRequestItem> items,
        CancellationToken cancellationToken = default)
    {
        var maxParallel = Math.Clamp(_ocrOptions.MaxParallel, 1, 6);
        var gate = new SemaphoreSlim(maxParallel, maxParallel);
        var results = new BatchVerificationItemResult[items.Count];

        var tasks = items.Select(async (item, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                results[index] = await VerifyBatchItemAsync(item, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new BatchVerificationResult
        {
            Items = results.ToList(),
            SuccessCount = results.Count(r => r.Result is not null),
            FailureCount = results.Count(r => r.Error is not null)
        };
    }

    private async Task<BatchVerificationItemResult> VerifyBatchItemAsync(
        BatchVerificationRequestItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await VerifyAsync(
                item.Bytes,
                item.Expected,
                item.OcrText,
                item.UseClientOcr,
                skipServerOcrWhenClientTextPresent: false,
                cancellationToken);
            return new BatchVerificationItemResult { FileName = item.FileName, Result = result };
        }
        catch (TimeoutException)
        {
            return new BatchVerificationItemResult
            {
                FileName = item.FileName,
                Result = new VerificationResult
                {
                    OverallStatus = VerificationOutcome.Timeout,
                    IsVerified = false,
                    OverallConfidence = 0,
                    Fields = Array.Empty<FieldVerificationResult>(),
                    RawOcrText = string.Empty,
                    ProcessingTimeMs = _ocrOptions.TimeoutSeconds * 1000,
                    StatusMessage = "Processing exceeded the per-label time limit.",
                    AgentGuidance = "Retry with a smaller batch or a clearer image file from the applicant."
                }
            };
        }
        catch (Exception ex)
        {
            return new BatchVerificationItemResult { FileName = item.FileName, Error = ex.Message };
        }
    }
}

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddLabelVerificationCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<VerificationOptions>(configuration.GetSection(VerificationOptions.SectionName));
        services.Configure<ReadabilityOptions>(configuration.GetSection(ReadabilityOptions.SectionName));
        services.Configure<FlatLabelComplianceOptions>(configuration.GetSection(FlatLabelComplianceOptions.SectionName));
        services.Configure<ColaOptions>(configuration.GetSection(ColaOptions.SectionName));
        services.Configure<LayoutOptions>(configuration.GetSection(LayoutOptions.SectionName));
        services.AddSingleton<IColaPublicCache, ColaPublicCache>();
        services.AddSingleton<ILayoutAnnotationStore, LayoutAnnotationStore>();
        services.AddSingleton<OnnxLabelLayoutDetector>();
        services.AddSingleton<ILabelLayoutDetector, HybridLabelLayoutDetector>();
        services.AddSingleton<ILayoutGuidedOcrService, LayoutGuidedOcrService>();
        services.AddSingleton<ILabelFieldExtractor, LabelFieldExtractor>();
        services.AddScoped<IAutonomousVerificationService, AutonomousVerificationService>();
        services.AddSingleton<ITesseractEngineProvider, TesseractEngineProvider>();
        services.AddSingleton<IImagePreprocessor, OpenCvImagePreprocessor>();
        services.AddSingleton<IOcrReadabilityAssessor, OcrReadabilityAssessor>();
        services.AddSingleton<IFlatLabelComplianceAnalyzer, FlatLabelComplianceAnalyzer>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<IFieldMatcher, BrandNameMatcher>();
        services.AddSingleton<IFieldMatcher, ClassTypeMatcher>();
        services.AddSingleton<IFieldMatcher, AbvMatcher>();
        services.AddSingleton<IFieldMatcher, NetContentsMatcher>();
        services.AddSingleton<IFieldMatcher, BottlerAddressMatcher>();
        services.AddSingleton<IFieldMatcher, CountryOfOriginMatcher>();
        services.AddSingleton<IFieldMatcher, TtbWarningMatcher>();
        services.AddSingleton<IFieldMatcher, BoldTextMatcher>();
        services.AddSingleton<IFieldMatcher, AppellationMatcher>();
        services.AddSingleton<IFieldMatcher, VintageMatcher>();
        services.AddSingleton<IFieldMatcher, SulfiteDeclarationMatcher>();
        services.AddSingleton<IFieldMatcher, OrganicClaimMatcher>();
        services.AddSingleton<IFieldMatcher, BarcodeUpcMatcher>();
        services.AddScoped<IVerificationService, VerificationService>();
        return services;
    }
}
