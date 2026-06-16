import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

const cursorImagesDir = path.join(
  process.env.APPDATA ?? "",
  "Cursor",
  "User",
  "workspaceStorage",
  "8cae447fdcba937352e943f65161fd43",
  "images",
);

const TABC_ODP_DATASET_URL =
  "https://data.texas.gov/dataset/Approved-Product-Label-Search/2cjh-3vae";

const tabcCsvPath =
  process.env.TABC_CSV_PATH ??
  path.join(process.env.USERPROFILE ?? "", "Downloads", "Approved_Product_Label_Search_20260614.csv");

const parseCsvLine = (line) => {
  const parts = [];
  let current = "";
  let inQuotes = false;
  for (const char of line) {
    if (char === '"') {
      inQuotes = !inQuotes;
      continue;
    }
    if (char === "," && !inQuotes) {
      parts.push(current.trim());
      current = "";
      continue;
    }
    current += char;
  }
  parts.push(current.trim());
  return parts;
};

const parseCsvRowByTtbId = (csvText, ttbId) => {
  for (const line of csvText.split("\n")) {
    if (!line.includes(ttbId)) {
      continue;
    }

    const parts = parseCsvLine(line);
    const ttbNumbers = (parts[7] ?? "")
      .split(",")
      .map((value) => value.trim().replace(/"/g, ""))
      .filter(Boolean);

    if (!ttbNumbers.includes(ttbId)) {
      continue;
    }

    return {
      tabcCertificateNumber: parts[0],
      permitNumber: parts[1],
      productName: parts[2],
      productType: parts[3],
      approvalDate: parts[4],
      tradeName: parts[5],
      abvPercent: parts[6] ? Number(parts[6]) : null,
      ttbNumbers,
      ttbId,
      labelPdfUrl: parts[8] ?? null,
    };
  }

  return null;
};

const fixtures = [
  {
    ttbId: "13297001000322",
    imageFile: "image-7005e598-9813-46a0-815c-73ba3db56db2.png",
    sampleFile: "real-photo-remy-martin-club.png",
    label: "Rémy Martin Club Fine Champagne Cognac (real front photo)",
    userPhotoMatchesTabcRow: false,
    tabcCsvNote:
      "Rémy Martin Club is not listed in the TABC export; TTB ID 13297001000322 is the nearest Rémy Martin cognac approval (same importer permit NY-I-892). Expected fanciful/class fields come from the bottle front label.",
    brandName: "REMY MARTIN",
    fancifulName: "CLUB",
    classTypeDesignation: "FINE CHAMPAGNE COGNAC",
    abvPercent: 40,
    netContents: "70 CL",
    bottlerProducerAddress: "REMY MARTIN & CO, COGNAC FRANCE",
    countryOfOrigin: "France",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: ["BrandName", "ClassTypeDesignation"],
  },
  {
    ttbId: "14106001000237",
    imageFile: "image-e4ef963f-b4e6-4c5c-a2b2-2ffe6bb4b9b9.png",
    sampleFile: "real-photo-ambhar-plata.png",
    label: "Ambhar Plata Tequila (real front photo)",
    userPhotoMatchesTabcRow: true,
    tabcCsvProductName: "AMBHAR - PLATA TEQUILA",
    brandName: "AMBHAR",
    fancifulName: "PLATA",
    classTypeDesignation: "TEQUILA",
    abvPercent: 40,
    netContents: "750 mL",
    bottlerProducerAddress: "Imported by Ambhar Global Spirits LLC",
    countryOfOrigin: "Mexico",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: ["BrandName", "AbvPercent", "NetContents"],
  },
  {
    ttbId: "18303001000896",
    imageFile: "image-41a59747-13e7-4f04-b586-a7c34542df0a.png",
    sampleFile: "real-photo-leopards-leap-chardonnay.png",
    label: "Leopard's Leap Unwooded Chardonnay 2017 (real front photo)",
    userPhotoMatchesTabcRow: true,
    tabcCsvProductName: "LA MOTTE 2017 WHITE CHARDONNAY, FRANSCHH",
    brandName: "LEOPARD'S LEAP",
    fancifulName: "UNWOODED CHARDONNAY",
    classTypeDesignation: "UNWOODED CHARDONNAY",
    abvPercent: 12.5,
    netContents: "750 mL",
    bottlerProducerAddress: "LEOPARD'S LEAP FAMILY VINEYARDS, SOUTH AFRICA",
    countryOfOrigin: "South Africa",
    productCategory: "wine",
    expectedFieldsToPass: ["BrandName", "ClassTypeDesignation", "CountryOfOrigin"],
  },
  {
    ttbId: "21194001000323",
    imageFile: "image-7db56d75-73f8-4ce5-b54b-9d9b1fe3eb29.png",
    sampleFile: "real-photo-act-of-treason-blanco.png",
    label: "Act of Treason Australian Agave Spirit Blanco (real front photo)",
    userPhotoMatchesTabcRow: false,
    tabcCsvNote:
      "Act of Treason is not listed in the Texas ODP export. Fixture TTB ID 21194001000323 is a separate approved agave spirit row; its File Link is an exact 1:1 match for that TTB Number only. Visible label fields on the user photo come from the bottle, not from the File Link artwork.",
    brandName: "ACT OF TREASON",
    fancifulName: "BLANCO",
    classTypeDesignation: "AUSTRALIAN AGAVE SPIRIT",
    abvPercent: 40,
    netContents: "700 mL",
    bottlerProducerAddress: "Top Shelf International, Queensland, Australia",
    countryOfOrigin: "Australia",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: ["BrandName", "ClassTypeDesignation", "AbvPercent", "NetContents"],
  },
  {
    ttbId: "18055001000023",
    imageFile: "image-625b68ca-4e1a-4223-8f8b-ff7e6f97cdab.png",
    sampleFile: "real-photo-juniper-tree-gin.png",
    label: "The Juniper Tree Gin / House of Porfidio (real front photo)",
    userPhotoMatchesTabcRow: false,
    tabcCsvNote:
      "The Juniper Tree Gin is not listed in the Texas ODP export. Fixture TTB ID 18055001000023 (Copper Kings Moons of Juniper Gin) is a separate approved row; its File Link matches that TTB Number exactly. Visible label fields on the user photo come from the bottle, not from the File Link artwork.",
    brandName: "THE JUNIPER TREE",
    fancifulName: "BOUQUET OF DESERT FLOWERS",
    classTypeDesignation: "GIN",
    abvPercent: 47,
    netContents: "500 mL",
    bottlerProducerAddress: "THE HOUSE OF PORFIDIO",
    countryOfOrigin: "Mexico",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: ["BrandName", "ClassTypeDesignation", "AbvPercent", "NetContents"],
  },
  {
    ttbId: "14086001000323",
    imageFile: "image-9e216fd3-d702-4a66-a70f-2d64e64c1228.png",
    sampleFile: "real-photo-la-venenosa-raicilla.png",
    label: "La Venenosa Raicilla Sierra Occidental (real front photo)",
    userPhotoMatchesTabcRow: true,
    tabcCsvProductName: "LA VENENOSA RAICILLA COSTA DE JALISCO DO",
    brandName: "LA VENENOSA",
    fancifulName: "RAICILLA",
    classTypeDesignation: "RAICILLA",
    abvPercent: 42.5,
    netContents: "700 mL",
    bottlerProducerAddress: "Imported by MHW LTD.",
    countryOfOrigin: "Mexico",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: [],
  },
  {
    ttbId: "13343001000271",
    imageFile: "image-5804d57d-24db-4d47-9cc1-30421bff5920.png",
    sampleFile: "real-photo-jack-daniels-old-no7.png",
    label: "Jack Daniel's Old No. 7 Tennessee Whiskey (real front photo)",
    userPhotoMatchesTabcRow: true,
    tabcCsvProductName: "JACK DANIEL'S TENNESSEE WHISKEY",
    brandName: "JACK DANIEL",
    fancifulName: "Old No. 7 BRAND",
    classTypeDesignation: "TENNESSEE WHISKEY",
    abvPercent: 40,
    netContents: "1.0 L",
    bottlerProducerAddress: "DISTILLED AND BOTTLED BY JACK DANIEL DISTILLERY, LYNCHBURG, TENNESSEE, USA",
    countryOfOrigin: "United States",
    productCategory: "distilled_spirits",
    expectedFieldsToPass: ["BrandName", "ClassTypeDesignation", "AbvPercent", "NetContents"],
  },
];

const resolveSourceImage = async (imageFile) => {
  const candidates = [
    path.join(cursorImagesDir, imageFile),
    path.join(
      root,
      "..",
      ".cursor",
      "projects",
      "c-Users-Alaina-Documents-AWS-Job-Hunt",
      "assets",
      `c__Users_Alaina_AppData_Roaming_Cursor_User_workspaceStorage_8cae447fdcba937352e943f65161fd43_images_${imageFile.replace(/-/g, "_")}`,
    ),
  ];

  for (const candidate of candidates) {
    try {
      await fs.access(candidate);
      return candidate;
    } catch {
      // try next
    }
  }

  return null;
};

const buildMeta = (fixture, csvRow) => {
  const colaRegistryUrl = `https://ttbonline.gov/colasonline/viewColaDetails.do?action=publicFormDisplay&ttbid=${fixture.ttbId}`;
  const tabcCsvExactTtbMatch = csvRow !== null;
  const userPhotoMatchesTabcRow = fixture.userPhotoMatchesTabcRow ?? tabcCsvExactTtbMatch;
  const approvedLabelFileLink = tabcCsvExactTtbMatch ? csvRow.labelPdfUrl : null;

  return {
    ttbId: fixture.ttbId,
    fancifulName: fixture.fancifulName,
    brandName: fixture.brandName,
    origin: fixture.countryOfOrigin,
    originType: fixture.countryOfOrigin === "United States" ? "domestic" : "import",
    classType: fixture.classTypeDesignation,
    productCategory: fixture.productCategory,
    colaRegistryUrl,
    colaRegistrySearchHint: `Search TTB ID ${fixture.ttbId} at Public COLA Registry`,
    approvedLabelSearchSource: "texas_odp_approved_product_label_search",
    approvedLabelSearchUrl: TABC_ODP_DATASET_URL,
    approvedLabelSearchDataPreviewUrl: `${TABC_ODP_DATASET_URL}/data_preview`,
    tabcCsvExactTtbMatch,
    tabcCsvMatched: tabcCsvExactTtbMatch,
    userPhotoMatchesTabcRow,
    tabcCsvPath: tabcCsvPath,
    tabcCsvProductName: fixture.tabcCsvProductName ?? csvRow?.productName ?? null,
    tabcCertificateNumber: csvRow?.tabcCertificateNumber ?? null,
    tabcApprovedLabelFileLink: approvedLabelFileLink,
    tabcLabelPdfUrl: approvedLabelFileLink,
    tabcFileLinkMatchesTtbNumberExactly: tabcCsvExactTtbMatch,
    tabcApprovedLabelFileLinkNote: tabcCsvExactTtbMatch
      ? "Texas ODP File Link is the approved label artwork for this TTB Number on that row (1:1). See https://data.texas.gov/dataset/Approved-Product-Label-Search/2cjh-3vae/data_preview"
      : null,
    tabcCsvNote: fixture.tabcCsvNote ?? null,
    representativeFieldsNote:
      userPhotoMatchesTabcRow
        ? "Real non-synthetic bottle front photo paired with a Texas ODP row whose TTB Number matches this fixture. OCR should match visible identity fields but fail overall verification because the government warning and bold phrase are not on this front-label view."
        : "Real non-synthetic bottle front photo. Expected label fields are read from the photo. When a Texas ODP File Link is present, it matches the fixture TTB Number exactly but may depict a different approved product than the photo.",
    imageSource: "tabc_csv_real_photo",
    imageSourceNotes: tabcCsvExactTtbMatch
      ? userPhotoMatchesTabcRow
        ? `Paired with Texas ODP Approved Product Label Search row for TTB ${fixture.ttbId}; File Link matches that TTB Number exactly.`
        : `Texas ODP row exists for TTB ${fixture.ttbId} (File Link is 1:1 with that TTB Number), but the user photo is a different product.`
      : `No Texas ODP row for TTB ${fixture.ttbId}; ${fixture.tabcCsvNote ?? "COLA metadata is derived from the bottle photo."}`,
    expectVerificationPass: false,
    expectedFieldsToPass: fixture.expectedFieldsToPass,
    expectedFieldsToFail: ["TtbWarningText", "BoldWarningPhrase"],
    expectedLabelFields: {
      brandName: fixture.brandName,
      fancifulName: fixture.fancifulName,
      classTypeDesignation: fixture.classTypeDesignation,
      abvPercent: fixture.abvPercent,
      netContents: fixture.netContents,
      bottlerProducerAddress: fixture.bottlerProducerAddress,
      countryOfOrigin: fixture.countryOfOrigin,
      productCategory: fixture.productCategory,
      labelPresentation: "realBottleFrontWithWarningCheck",
      ttbWarningText: ttbWarning,
      boldWarningPhrase: "GOVERNMENT WARNING:",
    },
  };
};

let csvText = "";
try {
  csvText = await fs.readFile(tabcCsvPath, "utf8");
} catch {
  console.warn(`TABC CSV not found at ${tabcCsvPath}; writing label-only metadata.`);
}

await fs.mkdir(colasDir, { recursive: true });

for (const fixture of fixtures) {
  const sourcePath = await resolveSourceImage(fixture.imageFile);
  if (!sourcePath) {
    console.warn(`Skip ${fixture.ttbId}: missing source image ${fixture.imageFile}`);
    continue;
  }

  const csvRow = csvText ? parseCsvRowByTtbId(csvText, fixture.ttbId) : null;
  const destImagePath = path.join(colasDir, `${fixture.ttbId}.png`);
  const destMetaPath = path.join(colasDir, `${fixture.ttbId}.meta.json`);

  await fs.copyFile(sourcePath, destImagePath);
  await fs.writeFile(destMetaPath, `${JSON.stringify(buildMeta(fixture, csvRow), null, 2)}\n`);

  console.log(`Installed ${fixture.ttbId} from ${sourcePath}`);
}

console.log("Run pnpm setup:reviewer-pack to refresh the 5 demo Load samples.");
