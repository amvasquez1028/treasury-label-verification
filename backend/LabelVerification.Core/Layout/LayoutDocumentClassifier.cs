using LabelVerification.Core.Compliance;
using LabelVerification.Core.Ocr;
using OpenCvSharp;

namespace LabelVerification.Core.Layout;

internal static class LayoutDocumentClassifier
{
    internal static DocumentLayoutClass Classify(byte[] imageBytes, IImagePreprocessor preprocessor)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var kind = preprocessor.ClassifyImage(imageBytes);
        var aspect = mat.Height / (double)Math.Max(1, mat.Width);

        if (kind == LabelImageKind.Screenshot)
        {
            return DocumentLayoutClass.Screenshot;
        }

        if (kind == LabelImageKind.BottlePhoto)
        {
            return DocumentLayoutClass.BottlePhoto;
        }

        if (aspect > 2.0)
        {
            var pages = StackedTabcPageHelper.Split(imageBytes);
            return pages.Count switch
            {
                >= 3 => DocumentLayoutClass.OdpStack3Page,
                2 => DocumentLayoutClass.OdpStack2Page,
                _ => DocumentLayoutClass.OdpFlatLabel,
            };
        }

        return DocumentLayoutClass.OdpFlatLabel;
    }

    internal static float[] ToFeatureVector(DocumentLayoutClass documentClass, byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        var aspect = mat.Height / (double)Math.Max(1, mat.Width);
        var pageCount = StackedTabcPageHelper.Split(imageBytes).Count;
        return
        [
            documentClass == DocumentLayoutClass.OdpStack3Page ? 1f : 0f,
            documentClass == DocumentLayoutClass.OdpStack2Page ? 1f : 0f,
            documentClass == DocumentLayoutClass.OdpFlatLabel ? 1f : 0f,
            documentClass == DocumentLayoutClass.BottlePhoto ? 1f : 0f,
            documentClass == DocumentLayoutClass.Screenshot ? 1f : 0f,
            (float)Math.Clamp(aspect / 4.0, 0, 1),
            (float)Math.Clamp(pageCount / 4.0, 0, 1),
            mat.Width / 4000f,
            mat.Height / 8000f,
        ];
    }
}
