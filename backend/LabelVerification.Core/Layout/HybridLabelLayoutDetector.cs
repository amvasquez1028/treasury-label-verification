using LabelVerification.Core.Ocr;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Layout;

public sealed class HybridLabelLayoutDetector : ILabelLayoutDetector
{
    private readonly IImagePreprocessor _preprocessor;
    private readonly AnnotationAnchorLayoutDetector _anchorDetector;
    private readonly OnnxLabelLayoutDetector _onnxDetector;
    private readonly LayoutOptions _options;

    public HybridLabelLayoutDetector(
        IImagePreprocessor preprocessor,
        ILayoutAnnotationStore store,
        OnnxLabelLayoutDetector onnxDetector,
        IOptions<LayoutOptions> options)
    {
        _preprocessor = preprocessor;
        _anchorDetector = new AnnotationAnchorLayoutDetector(store, preprocessor);
        _onnxDetector = onnxDetector;
        _options = options.Value;
    }

    public LayoutDetectionResult Detect(byte[] imageBytes)
    {
        var documentClass = LayoutDocumentClassifier.Classify(imageBytes, _preprocessor);

        if (_options.PreferOnnx && _onnxDetector.IsAvailable)
        {
            var onnx = _onnxDetector.TryDetect(imageBytes, documentClass);
            if (onnx is not null && onnx.OverallConfidence >= _options.MinRegionConfidence)
            {
                return onnx;
            }
        }

        var anchor = _anchorDetector.Detect(imageBytes, documentClass);
        if (anchor.Regions.Count > 0)
        {
            return anchor;
        }

        return HeuristicLayoutFallback.Detect(imageBytes, documentClass, _preprocessor);
    }
}
