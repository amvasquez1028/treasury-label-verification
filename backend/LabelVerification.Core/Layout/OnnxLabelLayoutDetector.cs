using LabelVerification.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LabelVerification.Core.Layout;

public sealed class OnnxLabelLayoutDetector : IDisposable
{
    private readonly ILayoutAnnotationStore _store;
    private readonly LayoutOptions _options;
    private readonly ILogger<OnnxLabelLayoutDetector> _logger;
    private readonly InferenceSession? _session;
    private readonly bool _isAvailable;

    public OnnxLabelLayoutDetector(
        ILayoutAnnotationStore store,
        IOptions<LayoutOptions> options,
        ILogger<OnnxLabelLayoutDetector> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;

        var modelPath = ResolveModelPath(_options.ModelPath);
        if (modelPath is null)
        {
            _session = null;
            _isAvailable = false;
            return;
        }

        try
        {
            _session = new InferenceSession(modelPath);
            _isAvailable = true;
            _logger.LogInformation("ONNX layout model loaded from {Path}", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ONNX layout model from {Path}", modelPath);
            _session = null;
            _isAvailable = false;
        }
    }

    public bool IsAvailable => _isAvailable;

    public LayoutDetectionResult? TryDetect(byte[] imageBytes, DocumentLayoutClass documentClass)
    {
        if (_session is null)
        {
            return null;
        }

        try
        {
            var features = LayoutDocumentClassifier.ToFeatureVector(documentClass, imageBytes);
            var input = new DenseTensor<float>(features, [1, features.Length]);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("features", input),
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();
            if (output.Length < 5)
            {
                return null;
            }

            var regions = DecodeRegions(output, documentClass);
            if (regions.Count == 0)
            {
                return null;
            }

            return new LayoutDetectionResult(documentClass, regions, 0.88, "onnx-v1");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX layout inference failed");
            return null;
        }
    }

    private IReadOnlyList<LabelFieldRegion> DecodeRegions(float[] output, DocumentLayoutClass documentClass)
    {
        if (!_store.AnchorTemplates.TryGetValue(documentClass, out var anchors))
        {
            return [];
        }

        var regions = new List<LabelFieldRegion>();
        var fieldCount = Math.Min(anchors.Count, output.Length / 5);
        for (var i = 0; i < fieldCount; i += 1)
        {
            var offset = i * 5;
            var anchor = anchors[i];
            var x = Clamp01(output[offset + 0]);
            var y = Clamp01(output[offset + 1]);
            var w = Clamp01(output[offset + 2]);
            var h = Clamp01(output[offset + 3]);
            var confidence = Clamp01(output[offset + 4]);

            if (confidence < _options.MinRegionConfidence * 0.5)
            {
                x = anchor.X;
                y = anchor.Y;
                w = anchor.Width;
                h = anchor.Height;
                confidence = 0.75f;
            }

            regions.Add(new LabelFieldRegion(
                anchor.Field,
                x,
                y,
                w,
                h,
                anchor.PageIndex,
                confidence,
                "onnx-v1"));
        }

        return regions;
    }

    private static double Clamp01(float value) => Math.Clamp(value, 0.0, 1.0);

    private static string? ResolveModelPath(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var candidates = new List<string> { configured };
        candidates.Add(Path.Combine("testdata", "layout-models", "label-layout-v1.onnx"));

        var baseDir = AppContext.BaseDirectory;
        for (var depth = 0; depth <= 6; depth += 1)
        {
            var parts = new List<string> { baseDir };
            for (var i = 0; i < depth; i += 1)
            {
                parts.Add("..");
            }

            parts.Add("testdata");
            parts.Add("layout-models");
            parts.Add("label-layout-v1.onnx");
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

    public void Dispose()
    {
        _session?.Dispose();
    }
}
