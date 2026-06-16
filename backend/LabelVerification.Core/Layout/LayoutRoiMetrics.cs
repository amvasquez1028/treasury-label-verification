using LabelVerification.Core.Layout;

namespace LabelVerification.Core.Layout;

public static class LayoutRoiMetrics
{
    public static double ComputeIoU(LabelFieldRegion predicted, LayoutAnchorRegion groundTruth)
    {
        var x1 = Math.Max(predicted.X, groundTruth.X);
        var y1 = Math.Max(predicted.Y, groundTruth.Y);
        var x2 = Math.Min(predicted.Right, groundTruth.X + groundTruth.Width);
        var y2 = Math.Min(predicted.Bottom, groundTruth.Y + groundTruth.Height);

        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var predictedArea = predicted.Width * predicted.Height;
        var truthArea = groundTruth.Width * groundTruth.Height;
        var union = predictedArea + truthArea - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    public static LayoutRoiQualityReport EvaluateSample(
        LayoutAnnotationSample sample,
        LayoutDetectionResult detection)
    {
        var perField = new List<LayoutFieldIoU>();
        foreach (var truth in sample.Regions)
        {
            var predicted = detection.Regions
                .Where(r => r.Field == truth.Field && r.PageIndex == truth.PageIndex)
                .ToList();

            var bestIoU = predicted.Count == 0
                ? 0
                : predicted.Max(p => ComputeIoU(p, truth));

            perField.Add(new LayoutFieldIoU(truth.Field.ToString(), bestIoU));
        }

        var meanIoU = perField.Count == 0 ? 0 : perField.Average(f => f.IoU);
        return new LayoutRoiQualityReport(sample.File, meanIoU, perField);
    }
}

public sealed record LayoutFieldIoU(string Field, double IoU);

public sealed record LayoutRoiQualityReport(
    string File,
    double MeanIoU,
    IReadOnlyList<LayoutFieldIoU> Fields);
