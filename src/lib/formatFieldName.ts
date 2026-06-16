const FIELD_DISPLAY_NAMES: Record<string, string> = {
  TtbWarningText: "Warning Text",
  BoldWarningPhrase: "Bold Phrase",
  WarningPlacement: "Warning Placement",
  WarningContrast: "Warning Contrast",
  BoldWarningTypography: "Bold Warning Typography",
  LabelTextContrast: "Label Text Contrast",
  SulfiteDeclaration: "Sulfite Declaration",
  BarcodeUpc: "Barcode / UPC",
};

export const formatFieldName = (fieldName: string): string => {
  const override = FIELD_DISPLAY_NAMES[fieldName];
  if (override) {
    return override;
  }

  const withSpaces = fieldName.replace(/([a-z])([A-Z])/g, "$1 $2");
  return withSpaces.replace(/([A-Z])([A-Z][a-z])/g, "$1 $2");
};
