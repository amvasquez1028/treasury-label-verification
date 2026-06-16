import type { ColaCacheFields, ExpectedLabelFields } from "@/lib/api";

export const parseColaCachePayload = (
  payload: ColaCacheFields,
): { fancifulName: string; expected: ExpectedLabelFields } => {
  const fancifulName = payload.fancifulName?.trim() ?? "";

  return {
    fancifulName,
    expected: {
      brandName: payload.brandName,
      ...(fancifulName ? { fancifulName } : {}),
      classTypeDesignation: payload.classTypeDesignation,
      abvPercent: payload.abvPercent,
      netContents: payload.netContents,
      bottlerProducerAddress: payload.bottlerProducerAddress,
      countryOfOrigin: payload.countryOfOrigin ?? "",
      productCategory: payload.productCategory,
      ttbWarningText: payload.ttbWarningText,
      boldWarningPhrase: payload.boldWarningPhrase,
    },
  };
};
