import sharp from "sharp";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const fixturesDir = path.join(root, "testdata", "fixtures");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

const labels = [
  {
    slug: "baseline-01",
    brand: "Blue Ridge Bourbon",
    classType: "Straight Bourbon Whiskey",
    abv: 40.0,
    netContents: "750 mL",
    bottlerAddress: "Distilled and Bottled by Blue Ridge Distillery, Asheville, NC",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "baseline-02",
    brand: "Harbor Light Gin",
    classType: "Distilled Gin",
    abv: 42.5,
    netContents: "750 mL",
    bottlerAddress: "Distilled and Bottled by Harbor Light Spirits, Portland, OR",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "baseline-03",
    brand: "Copper Still Rye",
    classType: "Straight Rye Whiskey",
    abv: 45.0,
    netContents: "750 mL",
    bottlerAddress: "Distilled and Bottled by Copper Still Distillery, Louisville, KY",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-04",
    brand: "Blue Ridge Bourbon",
    classType: "Straight Bourbon Whiskey",
    abv: 40.0,
    netContents: "750 mL",
    bottlerAddress: "Distilled and Bottled by Blue Ridge Distillery, Asheville, NC",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-05",
    brand: "Summit Creek Vodka",
    classType: "Vodka",
    abv: 35.0,
    netContents: "750 mL",
    bottlerAddress: "Distilled and Bottled by Summit Creek Distillery, Denver, CO",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-06",
    brand: "Old Mill Whiskey",
    classType: "Blended Whiskey",
    abv: 43.0,
    netContents: "750 mL",
    bottlerAddress: "Bottled by Old Mill Spirits, Nashville, TN",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-07",
    brand: "Riverbank Rum",
    classType: "Caribbean Rum",
    abv: 38.0,
    netContents: "750 mL",
    bottlerAddress: "Imported by Riverbank Imports, Miami, FL",
    countryOfOrigin: "Jamaica",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-08",
    brand: "Granite Peak Tequila",
    classType: "Tequila",
    abv: 40.0,
    netContents: "750 mL",
    bottlerAddress: "Imported by Granite Peak Beverages, Phoenix, AZ",
    countryOfOrigin: "Mexico",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-09",
    brand: "Prairie Fire Brandy",
    classType: "Brandy",
    abv: 36.5,
    netContents: "750 mL",
    bottlerAddress: "Produced and Bottled by Prairie Fire Cellars, Lincoln, NE",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-10",
    brand: "Timberline Liqueur",
    classType: "Liqueur",
    abv: 30.0,
    netContents: "750 mL",
    bottlerAddress: "Produced and Bottled by Timberline Spirits, Boise, ID",
    countryOfOrigin: "",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-11",
    brand: "Silver Oak Absinthe",
    classType: "Absinthe",
    abv: 55.0,
    netContents: "750 mL",
    bottlerAddress: "Imported by Silver Oak Imports, Seattle, WA",
    countryOfOrigin: "France",
    productCategory: "distilled_spirits",
    noise: false,
  },
  {
    slug: "variant-12",
    brand: "North Star Malt",
    classType: "Malt Beverage",
    abv: 5.2,
    netContents: "12 fl oz",
    bottlerAddress: "Brewed and Packaged by North Star Brewing, Minneapolis, MN",
    countryOfOrigin: "",
    productCategory: "beer",
    noise: false,
  },
];

const escapeXml = (value) =>
  value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

const wrapWarning = (text, lineLength = 70) => {
  const words = text.split(" ");
  const lines = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > lineLength) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
};

const wrapText = (text, lineLength = 55) => {
  const words = text.split(" ");
  const lines = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > lineLength) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
};

const buildSvg = ({ brand, classType, abv, netContents, bottlerAddress, countryOfOrigin, noise }) => {
  const grain = noise
    ? `<filter id="n"><feTurbulence baseFrequency="0.9" numOctaves="2" result="t"/><feColorMatrix in="t" type="saturate" values="0"/></filter>`
    : "";
  const filter = noise ? ` filter="url(#n)"` : "";
  const warningLines = wrapWarning(ttbWarning, 55);
  const warningSvg = warningLines
    .map((line, index) => {
      const weight = index === 0 ? ' font-weight="700"' : "";
      return `<text x="50" y="${920 + index * 28}" font-family="Arial, Helvetica, sans-serif" font-size="20"${weight} fill="#111">${escapeXml(line)}</text>`;
    })
    .join("\n");

  const addressLines = wrapText(bottlerAddress, 50);
  const addressSvg = addressLines
    .map(
      (line, index) =>
        `<text x="450" y="${520 + index * 26}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="18" fill="#333">${escapeXml(line)}</text>`,
    )
    .join("\n");

  const countrySvg = countryOfOrigin
    ? `<text x="450" y="${520 + addressLines.length * 26 + 20}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="18" fill="#333">Product of ${escapeXml(countryOfOrigin)}</text>`
    : "";

  return `<svg width="900" height="1200" xmlns="http://www.w3.org/2000/svg">
  <defs>${grain}</defs>
  <rect width="100%" height="100%" fill="#faf7f2"${filter}/>
  <text x="450" y="100" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="52" font-weight="700" fill="#1a4480">${escapeXml(brand)}</text>
  <text x="450" y="165" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" fill="#333">${escapeXml(classType)}</text>
  <text x="450" y="230" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="34" fill="#333">${abv.toFixed(1)}% ALC. BY VOL.</text>
  <text x="450" y="290" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="26" fill="#333">${escapeXml(netContents)}</text>
  ${addressSvg}
  ${countrySvg}
  ${warningSvg}
</svg>`;
};

await fs.mkdir(fixturesDir, { recursive: true });

for (const label of labels) {
  const svg = buildSvg(label);
  const pngPath = path.join(fixturesDir, `${label.slug}.png`);
  const jsonPath = path.join(fixturesDir, `${label.slug}.json`);

  await sharp(Buffer.from(svg)).png().toFile(pngPath);
  const expected = {
    brandName: label.brand,
    classTypeDesignation: label.classType,
    abvPercent: label.abv,
    netContents: label.netContents,
    bottlerProducerAddress: label.bottlerAddress,
    productCategory: label.productCategory,
    ttbWarningText: ttbWarning,
    boldWarningPhrase: "GOVERNMENT WARNING:",
  };

  if (label.countryOfOrigin) {
    expected.countryOfOrigin = label.countryOfOrigin;
  }

  await fs.writeFile(jsonPath, JSON.stringify(expected, null, 2));
}

const unreadableBlankSvg = `<svg width="900" height="1200" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#fafafa"/></svg>`;
const unreadableGlareSvg = `<svg width="900" height="1200" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="#f8f8f8"/>
  <ellipse cx="450" cy="420" rx="400" ry="300" fill="#ffffff"/>
  <ellipse cx="200" cy="300" rx="180" ry="140" fill="#ffffff" opacity="0.85"/>
</svg>`;

const unreadableFixtures = [
  { slug: "unreadable-blank", svg: unreadableBlankSvg },
  { slug: "unreadable-glare", svg: unreadableGlareSvg },
];

for (const fixture of unreadableFixtures) {
  const pngPath = path.join(fixturesDir, `${fixture.slug}.png`);
  await sharp(Buffer.from(fixture.svg)).png().toFile(pngPath);
  await fs.writeFile(
    path.join(fixturesDir, `${fixture.slug}.json`),
    JSON.stringify(
      {
        brandName: "Placeholder Brand",
        classTypeDesignation: "Distilled Spirits",
        abvPercent: 40.0,
        netContents: "750 mL",
        bottlerProducerAddress: "Placeholder Distillery, Placeholder City, ST",
        productCategory: "distilled_spirits",
        ttbWarningText: ttbWarning,
        boldWarningPhrase: "GOVERNMENT WARNING:",
      },
      null,
      2,
    ),
  );
}

console.log(
  `Generated ${labels.length} label fixtures and ${unreadableFixtures.length} unreadable fixtures in ${fixturesDir}`,
);
