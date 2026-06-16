namespace LabelVerification.Core.Layout;

public sealed record LabelFieldRegion(
    LayoutFieldKind Field,
    double X,
    double Y,
    double Width,
    double Height,
    int PageIndex,
    double Confidence,
    string Source)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
