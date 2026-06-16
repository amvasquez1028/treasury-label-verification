using LabelVerification.Core.Layout;
using LabelVerification.Core.Models;

namespace LabelVerification.Core.Cola;

internal static class ColaExpectedResolver
{
    internal static ExpectedLabelFields ResolveForLayout(
        ColaCacheEntry entry,
        DocumentLayoutClass documentClass)
    {
        var baseExpected = entry.Expected with { LabelPresentation = LabelPresentation.FullLabel };
        if (entry.OdpExpected is null || !IsOdpDocumentLayout(documentClass))
        {
            return baseExpected;
        }

        var odp = entry.OdpExpected with { LabelPresentation = LabelPresentation.FullLabel };
        return baseExpected with
        {
            ClassTypeDesignation = odp.ClassTypeDesignation,
            NetContents = odp.NetContents,
            AbvPercent = odp.AbvPercent,
            BottlerProducerAddress = string.IsNullOrWhiteSpace(odp.BottlerProducerAddress)
                ? baseExpected.BottlerProducerAddress
                : odp.BottlerProducerAddress,
            FancifulName = string.IsNullOrWhiteSpace(odp.FancifulName)
                ? baseExpected.FancifulName
                : odp.FancifulName,
            CountryOfOrigin = string.IsNullOrWhiteSpace(odp.CountryOfOrigin)
                ? baseExpected.CountryOfOrigin
                : odp.CountryOfOrigin,
        };
    }

    private static bool IsOdpDocumentLayout(DocumentLayoutClass documentClass) =>
        documentClass is DocumentLayoutClass.OdpStack3Page
            or DocumentLayoutClass.OdpStack2Page
            or DocumentLayoutClass.OdpFlatLabel;
}
