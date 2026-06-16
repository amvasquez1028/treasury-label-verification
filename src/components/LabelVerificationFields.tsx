"use client";

import { useEffect, useState } from "react";
import { AutoGrowTextarea } from "@/components/AutoGrowTextarea";
import type { ExpectedLabelFields } from "@/lib/api";
import { defaultExpectedLabelFields } from "@/lib/defaultExpected";

type LabelVerificationFieldsProps = {
  expected: ExpectedLabelFields;
  onChange: (field: keyof ExpectedLabelFields, value: string) => void;
  fancifulName: string;
  onFancifulNameChange: (value: string) => void;
  prefillRevision?: number;
  idPrefix?: string;
};

const expectedFieldKeys: Array<keyof ExpectedLabelFields> = [
  "brandName",
  "classTypeDesignation",
  "abvPercent",
  "netContents",
  "bottlerProducerAddress",
  "countryOfOrigin",
  "productCategory",
  "ttbWarningText",
  "boldWarningPhrase",
];

const fieldInputClass = (isEdited: boolean, variant: "single" | "grow" = "single"): string => {
  const sizeClass = variant === "grow" ? "form-field-grow" : "form-field-single-row";
  return `form-field ${sizeClass} mt-1 w-full rounded border border-[var(--color-base-lighter)] px-3 py-2${
    isEdited ? " form-field-edited" : ""
  }`;
};

export const LabelVerificationFields = ({
  expected,
  onChange,
  fancifulName,
  onFancifulNameChange,
  prefillRevision = 0,
  idPrefix = "label",
}: LabelVerificationFieldsProps) => {
  const [editedFields, setEditedFields] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (prefillRevision === 0) {
      return;
    }

    const prefilled = new Set<string>();
    expectedFieldKeys.forEach((field) => {
      const value = expected[field];
      if (field === "abvPercent") {
        if (typeof value === "number" && value !== 0) {
          prefilled.add(field);
        }
        return;
      }
      if (typeof value === "string" && value.trim() !== "") {
        prefilled.add(field);
      }
    });
    if (fancifulName.trim() !== "") {
      prefilled.add("fancifulName");
    }

    setEditedFields(prefilled);
    // Prefill styling runs once per public-cache load (prefillRevision).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prefillRevision]);

  const handleFieldChange = (field: keyof ExpectedLabelFields, value: string) => {
    setEditedFields((current) => new Set(current).add(field));
    onChange(field, value);
  };

  const handleFieldFocus = (field: keyof ExpectedLabelFields) => {
    if (editedFields.has(field) || field === "productCategory") {
      return;
    }

    if (field === "abvPercent") {
      if (
        expected.abvPercent !== 0 &&
        expected.abvPercent === defaultExpectedLabelFields.abvPercent
      ) {
        setEditedFields((current) => new Set(current).add(field));
        onChange(field, "0");
      }
      return;
    }

    const currentValue = expected[field];
    const defaultValue = defaultExpectedLabelFields[field] as string;
    if (currentValue && currentValue === defaultValue) {
      setEditedFields((current) => new Set(current).add(field));
      onChange(field, "");
    }
  };

  const isEdited = (field: string): boolean => editedFields.has(field);

  const abvDisplayValue =
    !isEdited("abvPercent") && expected.abvPercent === 0 ? "" : expected.abvPercent;

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Brand name
        <input
          id={`${idPrefix}-brand`}
          value={expected.brandName}
          onChange={(event) => handleFieldChange("brandName", event.target.value)}
          onFocus={() => handleFieldFocus("brandName")}
          placeholder={defaultExpectedLabelFields.brandName}
          className={fieldInputClass(isEdited("brandName"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Fanciful name
        <input
          id={`${idPrefix}-fanciful`}
          value={fancifulName}
          onChange={(event) => onFancifulNameChange(event.target.value)}
          placeholder="Optional product name on label"
          className={fieldInputClass(editedFields.has("fancifulName"))}
          aria-label="Fanciful name"
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Class / type designation
        <input
          id={`${idPrefix}-class`}
          value={expected.classTypeDesignation}
          onChange={(event) => handleFieldChange("classTypeDesignation", event.target.value)}
          onFocus={() => handleFieldFocus("classTypeDesignation")}
          placeholder={defaultExpectedLabelFields.classTypeDesignation}
          className={fieldInputClass(isEdited("classTypeDesignation"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Product category
        <select
          id={`${idPrefix}-category`}
          value={expected.productCategory}
          onChange={(event) => handleFieldChange("productCategory", event.target.value)}
          onFocus={() => handleFieldFocus("productCategory")}
          className={fieldInputClass(isEdited("productCategory"))}
          aria-label="Product category"
        >
          <option value="distilled_spirits">Distilled spirits</option>
          <option value="wine">Wine</option>
          <option value="beer">Beer / malt beverage</option>
        </select>
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        ABV %
        <input
          id={`${idPrefix}-abv`}
          type="number"
          step="0.1"
          value={abvDisplayValue}
          onChange={(event) => handleFieldChange("abvPercent", event.target.value)}
          onFocus={() => handleFieldFocus("abvPercent")}
          placeholder={String(defaultExpectedLabelFields.abvPercent)}
          className={fieldInputClass(isEdited("abvPercent"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Net contents
        <input
          id={`${idPrefix}-net`}
          value={expected.netContents}
          onChange={(event) => handleFieldChange("netContents", event.target.value)}
          onFocus={() => handleFieldFocus("netContents")}
          placeholder={defaultExpectedLabelFields.netContents}
          className={fieldInputClass(isEdited("netContents"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Country of origin (imports only)
        <input
          id={`${idPrefix}-country`}
          value={expected.countryOfOrigin ?? ""}
          onChange={(event) => handleFieldChange("countryOfOrigin", event.target.value)}
          onFocus={() => handleFieldFocus("countryOfOrigin")}
          placeholder="Leave blank for domestic products"
          className={fieldInputClass(isEdited("countryOfOrigin"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        Bottler / producer name and address
        <input
          id={`${idPrefix}-bottler`}
          value={expected.bottlerProducerAddress}
          onChange={(event) => handleFieldChange("bottlerProducerAddress", event.target.value)}
          onFocus={() => handleFieldFocus("bottlerProducerAddress")}
          placeholder={defaultExpectedLabelFields.bottlerProducerAddress}
          className={fieldInputClass(isEdited("bottlerProducerAddress"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        TTB warning text
        <AutoGrowTextarea
          id={`${idPrefix}-warning`}
          value={expected.ttbWarningText}
          onChange={(event) => handleFieldChange("ttbWarningText", event.target.value)}
          onFocus={() => handleFieldFocus("ttbWarningText")}
          placeholder={defaultExpectedLabelFields.ttbWarningText}
          className={fieldInputClass(isEdited("ttbWarningText"), "grow")}
          aria-label="TTB warning text"
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        Bold warning phrase
        <input
          id={`${idPrefix}-bold`}
          value={expected.boldWarningPhrase}
          onChange={(event) => handleFieldChange("boldWarningPhrase", event.target.value)}
          onFocus={() => handleFieldFocus("boldWarningPhrase")}
          placeholder={defaultExpectedLabelFields.boldWarningPhrase}
          className={fieldInputClass(isEdited("boldWarningPhrase"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Appellation (wine/spirits, optional)
        <input
          id={`${idPrefix}-appellation`}
          value={expected.appellation ?? ""}
          onChange={(event) => handleFieldChange("appellation", event.target.value)}
          placeholder="Optional COLA appellation"
          className={fieldInputClass(isEdited("appellation"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)]">
        Vintage (optional)
        <input
          id={`${idPrefix}-vintage`}
          value={expected.vintage ?? ""}
          onChange={(event) => handleFieldChange("vintage", event.target.value)}
          placeholder="Optional vintage year"
          className={fieldInputClass(isEdited("vintage"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        Sulfite declaration (optional)
        <input
          id={`${idPrefix}-sulfites`}
          value={expected.sulfiteDeclaration ?? ""}
          onChange={(event) => handleFieldChange("sulfiteDeclaration", event.target.value)}
          placeholder="Contains sulfites"
          className={fieldInputClass(isEdited("sulfiteDeclaration"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        Organic claim (optional)
        <input
          id={`${idPrefix}-organic`}
          value={expected.organicClaim ?? ""}
          onChange={(event) => handleFieldChange("organicClaim", event.target.value)}
          placeholder="Optional organic certification text"
          className={fieldInputClass(isEdited("organicClaim"))}
        />
      </label>
      <label className="text-sm font-semibold text-[var(--color-base-darkest)] md:col-span-2">
        Barcode / UPC (optional)
        <input
          id={`${idPrefix}-barcode`}
          value={expected.barcodeUpc ?? ""}
          onChange={(event) => handleFieldChange("barcodeUpc", event.target.value)}
          placeholder="0 82184 09050 3"
          className={fieldInputClass(isEdited("barcodeUpc"))}
        />
      </label>
    </div>
  );
};
