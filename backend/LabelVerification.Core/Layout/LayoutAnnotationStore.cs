using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Layout;

public interface ILayoutAnnotationStore
{
    IReadOnlyList<LayoutAnnotationSample> AllSamples { get; }

    IReadOnlyDictionary<DocumentLayoutClass, IReadOnlyList<LayoutAnchorRegion>> AnchorTemplates { get; }

    bool TryGetSample(string fileName, out LayoutAnnotationSample sample);
}

public sealed record LayoutAnnotationSample(
    string File,
    string SampleKind,
    DocumentLayoutClass DocumentLayoutClass,
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<LayoutAnchorRegion> Regions);

public sealed record LayoutAnchorRegion(
    LayoutFieldKind Field,
    int PageIndex,
    double X,
    double Y,
    double Width,
    double Height);

internal sealed class LayoutAnnotationStore : ILayoutAnnotationStore
{
    private readonly IReadOnlyList<LayoutAnnotationSample> _samples;
    private readonly IReadOnlyDictionary<DocumentLayoutClass, IReadOnlyList<LayoutAnchorRegion>> _anchors;

    public LayoutAnnotationStore(IOptions<LayoutOptions> options, ILogger<LayoutAnnotationStore> logger)
    {
        var path = ResolveAnnotationsPath(options.Value.AnnotationsDir);
        if (path is null || !File.Exists(path))
        {
            logger.LogWarning("Layout annotations not found at {Path}", options.Value.AnnotationsDir);
            _samples = [];
            _anchors = new Dictionary<DocumentLayoutClass, IReadOnlyList<LayoutAnchorRegion>>();
            return;
        }

        var raw = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<AnnotationFile>(raw, JsonOptions);
        _samples = file?.Samples?.Select(MapSample).ToArray() ?? [];
        _anchors = BuildAnchorTemplates(_samples);
        logger.LogInformation(
            "Layout annotations loaded: {SampleCount} samples, {ClassCount} anchor templates from {Path}",
            _samples.Count,
            _anchors.Count,
            path);
    }

    public IReadOnlyList<LayoutAnnotationSample> AllSamples => _samples;

    public IReadOnlyDictionary<DocumentLayoutClass, IReadOnlyList<LayoutAnchorRegion>> AnchorTemplates => _anchors;

    public bool TryGetSample(string fileName, out LayoutAnnotationSample sample)
    {
        sample = _samples.FirstOrDefault(s =>
            string.Equals(s.File, fileName, StringComparison.OrdinalIgnoreCase))!;
        return sample is not null;
    }

    private static LayoutAnnotationSample MapSample(AnnotationSampleDto dto)
    {
        var layoutClass = Enum.TryParse<DocumentLayoutClass>(dto.DocumentLayoutClass, true, out var parsed)
            ? parsed
            : DocumentLayoutClass.Unknown;

        var regions = dto.Regions?.Select(r => new LayoutAnchorRegion(
            Enum.TryParse<LayoutFieldKind>(r.Field, true, out var field) ? field : LayoutFieldKind.BrandBlock,
            r.PageIndex,
            r.X,
            r.Y,
            r.Width,
            r.Height)).ToArray() ?? [];

        return new LayoutAnnotationSample(
            dto.File ?? string.Empty,
            dto.SampleKind ?? string.Empty,
            layoutClass,
            dto.ImageWidth,
            dto.ImageHeight,
            regions);
    }

    private static IReadOnlyDictionary<DocumentLayoutClass, IReadOnlyList<LayoutAnchorRegion>> BuildAnchorTemplates(
        IReadOnlyList<LayoutAnnotationSample> samples)
    {
        return samples
            .GroupBy(s => s.DocumentLayoutClass)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LayoutAnchorRegion>)AverageRegions(g.SelectMany(s => s.Regions)).ToArray());
    }

    private static IEnumerable<LayoutAnchorRegion> AverageRegions(IEnumerable<LayoutAnchorRegion> regions)
    {
        return regions
            .GroupBy(r => (r.Field, r.PageIndex))
            .Select(g =>
            {
                var list = g.ToList();
                return new LayoutAnchorRegion(
                    g.Key.Field,
                    g.Key.PageIndex,
                    list.Average(r => r.X),
                    list.Average(r => r.Y),
                    list.Average(r => r.Width),
                    list.Average(r => r.Height));
            })
            .OrderBy(r => r.Y)
            .ThenBy(r => r.X);
    }

    private static string? ResolveAnnotationsPath(string configured)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(Path.Combine(configured, "annotations.json"));
            candidates.Add(configured);
        }

        candidates.Add("testdata/layout-annotations/annotations.json");
        candidates.Add(Path.Combine("..", "testdata", "layout-annotations", "annotations.json"));

        var baseDir = AppContext.BaseDirectory;
        for (var depth = 0; depth <= 6; depth += 1)
        {
            var parts = new List<string> { baseDir };
            for (var i = 0; i < depth; i += 1)
            {
                parts.Add("..");
            }

            parts.Add("testdata");
            parts.Add("layout-annotations");
            parts.Add("annotations.json");
            candidates.Add(Path.Combine(parts.ToArray()));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed class AnnotationFile
    {
        public List<AnnotationSampleDto>? Samples { get; init; }
    }

    private sealed class AnnotationSampleDto
    {
        public string? File { get; init; }
        public string? SampleKind { get; init; }
        public string? DocumentLayoutClass { get; init; }
        public int ImageWidth { get; init; }
        public int ImageHeight { get; init; }
        public List<AnnotationRegionDto>? Regions { get; init; }
    }

    private sealed class AnnotationRegionDto
    {
        public string? Field { get; init; }
        public int PageIndex { get; init; }
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
    }
}
