import sharp from "sharp";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const outDir = path.join(root, "testdata", "fixtures", "old-tom-distillery");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

const expected = {
  brandName: "OLD TOM DISTILLERY",
  classTypeDesignation: "Kentucky Straight Bourbon Whiskey",
  abvPercent: 45.0,
  netContents: "750 mL",
  bottlerProducerAddress: "Distilled and Bottled by Old Tom Distillery, Lexington, KY",
  countryOfOrigin: "",
  productCategory: "distilled_spirits",
  ttbWarningText: ttbWarning,
  boldWarningPhrase: "GOVERNMENT WARNING:",
  expectedOutcome: "pass",
  notes: "README example distilled spirits label from take-home instructions",
};

const svg = `<svg width="900" height="1200" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="#f5f0e8"/>
  <text x="450" y="120" text-anchor="middle" font-family="Georgia, serif" font-size="42" font-weight="bold" fill="#1a1a1a">OLD TOM DISTILLERY</text>
  <text x="450" y="200" text-anchor="middle" font-family="Georgia, serif" font-size="24" fill="#333">Kentucky Straight Bourbon Whiskey</text>
  <text x="450" y="280" text-anchor="middle" font-family="Arial, sans-serif" font-size="22" fill="#333">45% Alc./Vol. (90 Proof)</text>
  <text x="450" y="340" text-anchor="middle" font-family="Arial, sans-serif" font-size="20" fill="#333">750 mL</text>
  <text x="450" y="400" text-anchor="middle" font-family="Arial, sans-serif" font-size="16" fill="#444">Distilled and Bottled by Old Tom Distillery, Lexington, KY</text>
  <text x="450" y="980" text-anchor="middle" font-family="Arial, sans-serif" font-size="11" font-weight="bold" fill="#000">${ttbWarning.slice(0, 80)}</text>
  <text x="450" y="1000" text-anchor="middle" font-family="Arial, sans-serif" font-size="10" fill="#000">${ttbWarning.slice(80, 160)}</text>
  <text x="450" y="1020" text-anchor="middle" font-family="Arial, sans-serif" font-size="10" fill="#000">${ttbWarning.slice(160)}</text>
</svg>`;

await fs.mkdir(outDir, { recursive: true });
await sharp(Buffer.from(svg)).png().toFile(path.join(outDir, "label.png"));
await fs.writeFile(
  path.join(outDir, "expected.json"),
  JSON.stringify(
    {
      brandName: expected.brandName,
      classTypeDesignation: expected.classTypeDesignation,
      abvPercent: expected.abvPercent,
      netContents: expected.netContents,
      bottlerProducerAddress: expected.bottlerProducerAddress,
      countryOfOrigin: expected.countryOfOrigin,
      productCategory: expected.productCategory,
      ttbWarningText: expected.ttbWarningText,
      boldWarningPhrase: expected.boldWarningPhrase,
      expectedOutcome: expected.expectedOutcome,
      notes: expected.notes,
    },
    null,
    2,
  ),
);

console.log("Wrote OLD TOM DISTILLERY fixture to", outDir);
