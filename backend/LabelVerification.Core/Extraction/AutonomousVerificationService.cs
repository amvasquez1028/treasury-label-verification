using LabelVerification.Core.Cola;
using LabelVerification.Core.Models;
using LabelVerification.Core.Options;
using LabelVerification.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Extraction;

public interface IAutonomousVerificationService
{
    Task<AutonomousVerificationResult> VerifyAsync(
        byte[] imageBytes,
        string? ttbIdHint = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutonomousVerificationResult>> VerifyBatchAsync(
        IReadOnlyList<(string FileName, byte[] Bytes, string? TtbIdHint)> items,
        CancellationToken cancellationToken = default);
}

public sealed class AutonomousVerificationService : IAutonomousVerificationService
{
    private readonly ILabelFieldExtractor _extractor;
    private readonly IVerificationService _verificationService;
    private readonly IColaPublicCache _colaCache;
    private readonly OcrOptions _ocrOptions;
    private readonly ILogger<AutonomousVerificationService> _logger;

    public AutonomousVerificationService(
        ILabelFieldExtractor extractor,
        IVerificationService verificationService,
        IColaPublicCache colaCache,
        IOptions<OcrOptions> ocrOptions,
        ILogger<AutonomousVerificationService> logger)
    {
        _extractor = extractor;
        _verificationService = verificationService;
        _colaCache = colaCache;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
    }

    public async Task<AutonomousVerificationResult> VerifyAsync(
        byte[] imageBytes,
        string? ttbIdHint = null,
        CancellationToken cancellationToken = default)
    {
        var extraction = await _extractor.ExtractAsync(imageBytes, cancellationToken);
        var resolvedTtbId = ResolveTtbId(ttbIdHint, extraction.Fields.TtbId);
        if (string.IsNullOrWhiteSpace(resolvedTtbId) || !_colaCache.TryGetEntry(resolvedTtbId, out var entry))
        {
            _logger.LogInformation(
                "Autonomous verify: COLA miss for TTB {TtbId}. Extracted brand={Brand}",
                resolvedTtbId ?? "(none)",
                extraction.Fields.BrandName);

            return new AutonomousVerificationResult
            {
                Extraction = extraction,
                ResolvedTtbId = resolvedTtbId,
                ColaRegistryHit = false,
                AgentGuidance = BuildColaMissGuidance(resolvedTtbId, extraction),
                Verification = new VerificationResult
                {
                    OverallStatus = VerificationOutcome.Review,
                    IsVerified = false,
                    OverallConfidence = extraction.Confidences.DefaultIfEmpty(new FieldExtractionConfidence { FieldName = "none", Confidence = 0 }).Min(c => c.Confidence),
                    Fields = BuildExtractOnlyFieldResults(extraction),
                    RawOcrText = extraction.RawOcrText,
                    ProcessingTimeMs = extraction.ProcessingTimeMs,
                    StatusMessage = "Demo COLA lookup: could not resolve an approved label in the local registry cache.",
                    AgentGuidance = "Autonomous mode is for demo/testing only. For production verification, enter treasury application values and use standard Verify (no TTB ID required).",
                },
            };
        }

        var expected = ColaExpectedResolver.ResolveForLayout(entry, extraction.Layout.DocumentClass);
        var verification = await _verificationService.VerifyAsync(
            imageBytes,
            expected,
            preExtractedOcrText: extraction.RawOcrText,
            skipServerOcrWhenClientTextPresent: true,
            cancellationToken: cancellationToken);

        return new AutonomousVerificationResult
        {
            Extraction = extraction,
            ResolvedTtbId = resolvedTtbId,
            ColaRegistryHit = true,
            Verification = verification,
            AgentGuidance = verification.OverallStatus == VerificationOutcome.Pass
                ? null
                : "Autonomous extraction completed; one or more fields did not match the approved COLA record.",
        };
    }

    public async Task<IReadOnlyList<AutonomousVerificationResult>> VerifyBatchAsync(
        IReadOnlyList<(string FileName, byte[] Bytes, string? TtbIdHint)> items,
        CancellationToken cancellationToken = default)
    {
        var maxParallel = Math.Clamp(_ocrOptions.MaxParallel, 1, 6);
        var gate = new SemaphoreSlim(maxParallel, maxParallel);
        var results = new AutonomousVerificationResult[items.Count];

        var tasks = items.Select(async (item, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                results[index] = await VerifyAsync(item.Bytes, item.TtbIdHint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Autonomous batch item failed: {File}", item.FileName);
                results[index] = new AutonomousVerificationResult
                {
                    Extraction = new LabelExtractionResult
                    {
                        Fields = new ExtractedLabelFields(),
                        Confidences = [],
                        RawOcrText = string.Empty,
                        RegionTexts = new Dictionary<string, string>(),
                        Layout = new Layout.LayoutDetectionResult(
                            Layout.DocumentLayoutClass.Unknown,
                            [],
                            0,
                            "error"),
                        ProcessingTimeMs = 0,
                    },
                    ResolvedTtbId = item.TtbIdHint,
                    ColaRegistryHit = false,
                    AgentGuidance = ex.Message,
                    Verification = new VerificationResult
                    {
                        OverallStatus = VerificationOutcome.Timeout,
                        IsVerified = false,
                        OverallConfidence = 0,
                        Fields = [],
                        RawOcrText = string.Empty,
                        ProcessingTimeMs = _ocrOptions.TimeoutSeconds * 1000,
                        StatusMessage = ex.Message,
                    },
                };
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private static string? ResolveTtbId(string? hint, string? extracted)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            return new string(hint.Where(char.IsDigit).ToArray());
        }

        return string.IsNullOrWhiteSpace(extracted) ? null : ColaTtbIdNormalizer.Normalize(extracted);
    }

    private static string BuildColaMissGuidance(string? ttbId, LabelExtractionResult extraction)
    {
        if (string.IsNullOrWhiteSpace(ttbId))
        {
            return "Demo COLA mode: TTB ID was not detected. Use standard Verify with application parameters instead.";
        }

        return $"Demo COLA mode: TTB ID {ttbId} is not in the local approved-label cache. Use standard Verify with treasury application values.";
    }

    private static IReadOnlyList<FieldVerificationResult> BuildExtractOnlyFieldResults(LabelExtractionResult extraction)
    {
        var fields = extraction.Fields;
        return
        [
            Field("TtbId", fields.TtbId),
            Field("BrandName", fields.BrandName),
            Field("ClassTypeDesignation", fields.ClassTypeDesignation),
            Field("AbvPercent", fields.AbvPercent?.ToString("0.0")),
            Field("NetContents", fields.NetContents),
            Field("BottlerProducerAddress", fields.BottlerProducerAddress),
            Field("CountryOfOrigin", fields.CountryOfOrigin),
            Field("TtbWarningText", fields.TtbWarningText),
        ];

        static FieldVerificationResult Field(string name, string? value) =>
            new()
            {
                FieldName = name,
                IsMatch = !string.IsNullOrWhiteSpace(value),
                Confidence = string.IsNullOrWhiteSpace(value) ? 0 : 0.85,
                ExtractedValue = value,
                ExpectedValue = null,
                Notes = "Extracted only — COLA registry lookup required for verification.",
            };
    }
}

internal static class ColaTtbIdNormalizer
{
    internal static string Normalize(string ttbId) =>
        new string(ttbId.Where(char.IsDigit).ToArray());
}
