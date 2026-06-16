namespace LabelVerification.Core.Layout;

public interface ILabelLayoutDetector
{
    LayoutDetectionResult Detect(byte[] imageBytes);
}
