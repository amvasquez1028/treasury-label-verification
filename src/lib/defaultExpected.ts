import type { ExpectedLabelFields } from "@/lib/api";

export const defaultExpectedLabelFields: ExpectedLabelFields = {
  brandName: "Blue Ridge Bourbon",
  classTypeDesignation: "Straight Bourbon Whiskey",
  abvPercent: 40.0,
  netContents: "750 mL",
  bottlerProducerAddress: "Distilled and Bottled by Blue Ridge Distillery, Asheville, NC",
  countryOfOrigin: "",
  productCategory: "distilled_spirits",
  ttbWarningText:
    "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.",
  boldWarningPhrase: "GOVERNMENT WARNING:",
};

export const cloneExpectedLabelFields = (): ExpectedLabelFields => ({
  brandName: "",
  classTypeDesignation: "",
  abvPercent: 0,
  netContents: "",
  bottlerProducerAddress: "",
  countryOfOrigin: "",
  productCategory: "distilled_spirits",
  ttbWarningText: "",
  boldWarningPhrase: "",
});
