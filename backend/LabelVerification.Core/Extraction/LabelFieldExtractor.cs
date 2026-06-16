using LabelVerification.Core.Layout;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Extraction;

public interface ILabelFieldExtractor
{
    Task<LabelExtractionResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

public sealed class LabelFieldExtractor : ILabelFieldExtractor
{
    private readonly ILabelLayoutDetector _layoutDetector;
    private readonly ILayoutGuidedOcrService _layoutOcr;
    private readonly IOcrService _ocrService;
    private readonly LayoutOptions _layoutOptions;

    public LabelFieldExtractor(
        ILabelLayoutDetector layoutDetector,
        ILayoutGuidedOcrService layoutOcr,
        IOcrService ocrService,
        IOptions<LayoutOptions> layoutOptions)
    {
        _layoutDetector = layoutDetector;
        _layoutOcr = layoutOcr;
        _ocrService = ocrService;
        _layoutOptions = layoutOptions.Value;
    }

    public async Task<LabelExtractionResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var started = Environment.TickCount64;
        var layout = _layoutDetector.Detect(imageBytes);
        var baselineOcr = await _ocrService.ExtractTextAsync(imageBytes, cancellationToken);
        var regionTexts = ShouldRunGuidedRoi(baselineOcr)
            ? await _layoutOcr.ExtractRegionTextsAsync(imageBytes, layout, cancellationToken)
            : (IReadOnlyDictionary<LayoutFieldKind, string>)new Dictionary<LayoutFieldKind, string>();
        var mergedOcr = MergeTexts(baselineOcr, regionTexts.Values);

        string RegionText(LayoutFieldKind kind) =>
            regionTexts.TryGetValue(kind, out var text) ? text : string.Empty;

        var ttbId = FieldTextParser.ExtractTtbId(RegionText(LayoutFieldKind.CertificateHeader))
            ?? FieldTextParser.ExtractTtbId(RegionText(LayoutFieldKind.TtbNumber))
            ?? FieldTextParser.ExtractTtbId(mergedOcr);

        var brand = FieldTextParser.ExtractBrand(RegionText(LayoutFieldKind.BrandBlock), mergedOcr);
        var classType = FieldTextParser.ExtractClassType(RegionText(LayoutFieldKind.ClassType), mergedOcr);
        var abv = FieldTextParser.ExtractAbv(RegionText(LayoutFieldKind.CertificateHeader))
            ?? FieldTextParser.ExtractAbv(RegionText(LayoutFieldKind.Abv))
            ?? FieldTextParser.ExtractAbv(mergedOcr);
        var netContents = FieldTextParser.ExtractNetContents(RegionText(LayoutFieldKind.CertificateHeader))
            ?? FieldTextParser.ExtractNetContents(RegionText(LayoutFieldKind.NetContents))
            ?? FieldTextParser.ExtractNetContents(mergedOcr);
        var bottler = FieldTextParser.ExtractBottlerAddress(RegionText(LayoutFieldKind.BottlerAddress), mergedOcr);
        var country = FieldTextParser.ExtractCountry(RegionText(LayoutFieldKind.CountryOfOrigin), mergedOcr);
        var hasWarning = FieldTextParser.HasGovernmentWarning(RegionText(LayoutFieldKind.GovernmentWarning))
            || FieldTextParser.HasGovernmentWarning(mergedOcr);

        var extracted = new ExtractedLabelFields
        {
            TtbId = ttbId,
            BrandName = brand,
            ClassTypeDesignation = classType,
            AbvPercent = abv,
            NetContents = netContents,
            BottlerProducerAddress = bottler,
            CountryOfOrigin = country,
            ProductCategory = FieldTextParser.InferProductCategory(classType),
            BoldWarningPhrase = hasWarning ? "GOVERNMENT WARNING:" : null,
            TtbWarningText = hasWarning
                ? "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems."
                : null,
        };

        var confidences = BuildConfidences(extracted, regionTexts);
        var regionTextMap = regionTexts.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value,
            StringComparer.Ordinal);

        return new LabelExtractionResult
        {
            Fields = extracted,
            Confidences = confidences,
            RawOcrText = mergedOcr,
            RegionTexts = regionTextMap,
            Layout = layout,
            ProcessingTimeMs = Environment.TickCount64 - started,
        };
    }

    private static IReadOnlyList<FieldExtractionConfidence> BuildConfidences(
        ExtractedLabelFields fields,
        IReadOnlyDictionary<LayoutFieldKind, string> regionTexts)
    {
        double Score(string? value, LayoutFieldKind kind) =>
            string.IsNullOrWhiteSpace(value) ? 0 : regionTexts.ContainsKey(kind) ? 0.9 : 0.72;

        return
        [
            new FieldExtractionConfidence { FieldName = "TtbId", Confidence = string.IsNullOrWhiteSpace(fields.TtbId) ? 0 : 0.95, Source = "certificate" },
            new FieldExtractionConfidence { FieldName = "BrandName", Confidence = Score(fields.BrandName, LayoutFieldKind.BrandBlock), Source = "brand-roi" },
            new FieldExtractionConfidence { FieldName = "ClassTypeDesignation", Confidence = Score(fields.ClassTypeDesignation, LayoutFieldKind.ClassType), Source = "class-roi" },
            new FieldExtractionConfidence { FieldName = "AbvPercent", Confidence = fields.AbvPercent.HasValue ? 0.92 : 0, Source = "certificate" },
            new FieldExtractionConfidence { FieldName = "NetContents", Confidence = Score(fields.NetContents, LayoutFieldKind.NetContents), Source = "certificate" },
            new FieldExtractionConfidence { FieldName = "BottlerProducerAddress", Confidence = Score(fields.BottlerProducerAddress, LayoutFieldKind.BottlerAddress), Source = "address-roi" },
            new FieldExtractionConfidence { FieldName = "CountryOfOrigin", Confidence = Score(fields.CountryOfOrigin, LayoutFieldKind.CountryOfOrigin), Source = "address-roi" },
            new FieldExtractionConfidence { FieldName = "TtbWarningText", Confidence = string.IsNullOrWhiteSpace(fields.TtbWarningText) ? 0 : 0.88, Source = "warning-roi" },
        ];
    }

    private static string MergeTexts(string baseline, IEnumerable<string> regionTexts)
    {
        var lines = baseline
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(regionTexts.SelectMany(t => t.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", lines);
    }

    private bool ShouldRunGuidedRoi(string baselineOcr)
    {
        if (!_layoutOptions.EnableGuidedRoiOcr)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(baselineOcr) || baselineOcr.Length < 250)
        {
            return true;
        }

        var hasTtb = FieldTextParser.ExtractTtbId(baselineOcr) is not null;
        var hasAbv = FieldTextParser.ExtractAbv(baselineOcr) is not null;
        if (hasTtb && hasAbv)
        {
            return false;
        }

        return baselineOcr.Length < 600;
    }
}
