# COLA test fixtures

Approved-label examples for automated verification tests. Metadata comes from the TTB Public COLA Registry (government public record). Images follow the sourcing strategy below.

## Example COLAs in this suite

| TTB ID | Fanciful | Brand | Origin | Class / Type |
|--------|----------|-------|--------|--------------|
| 03211001000018 | CASCADE VAL | CASCADE WINERY | Michigan, USA (domestic) | TABLE RED WINE |
| 11115001000373 | HONEY & BOURBON | BARENJAGER | Germany (import) | OTHER SPECIALTIES & PROPRIETARIES |
| 11364001000181 | DEBUTANTE | STILLWATER ARTISANAL | Maryland, USA (domestic) | MALT BEVERAGES SPECIALITIES - FLAVORED |
| 12207001000536 | ACHOLADO | VIJO TONEL | Peru (import) | OTHER GRAPE BRANDY (PISCO, GRAPPA) |
| 15107001000276 | STONE'S THROW | STONE'S THROW | Minnesota, USA (domestic) | STRAIGHT BOURBON WHISKY |

Each COLA has:

- `{ttbId}.meta.json` — registry fields plus `expectedLabelFields` for verification
- `{ttbId}.png` — label image used by OCR tests

**Representative fields:** `abvPercent` and `netContents` in `expectedLabelFields` are inferred from product class for OCR fixture testing. They are documented as representative in each meta file and are not asserted against the live COLA certificate.

## Image sourcing strategy

### 1. Preferred: manual download (Public COLA Registry)

1. Open the [Public COLA Registry](https://ttbonline.gov/colasonline/publicSearchColasBasic.do).
2. Search by **TTB ID** (see table above).
3. Open the approved certificate / printable view.
4. Save the label artwork as `testdata/colas/{ttbId}.png` (browser Print → Save as PNG, or screenshot of the printable certificate).

This uses government public records only — no commercial product photography.

### 2. Real bottle photos: `pnpm fetch:colas:photos`

Curated **Wikimedia Commons** bottle/can photos (PD, CC0, or CC-BY-SA) paired with COLA metadata for OCR on real-world images:

```bash
pnpm fetch:colas:photos
```

Uses direct thumbnail URLs (recommended by Wikimedia when bulk-fetching). Re-run later if rate-limited. Then rebuild samples:

```bash
pnpm setup:reviewer-pack
```

**Note:** Label text on a real product photo often differs from certificate fields in `expectedLabelFields`; samples pre-fill COLA metadata for demo verification workflows.

### 3. Script: `pnpm fetch:colas`

Run from repo root:

```bash
pnpm fetch:colas
# or replace existing PNGs when Commons matches are found:
pnpm fetch:colas:real
```

The script:

- Probes public COLA search endpoints (registry may require manual printable images).
- **Preserves** an existing `{ttbId}.png` unless `--force-real` is passed.
- **Generates a synthetic PNG** via Sharp when `{ttbId}.png` is missing — clearly marked **SYNTHETIC FIXTURE** on the image and `imageSource: "synthetic"` in meta.
- **Optionally** queries Wikimedia Commons; prefer `pnpm fetch:colas:photos` for reliable curated downloads.

### 4. Texas ODP real bottle fixtures: `pnpm install:tabc-real-bottles`

Seven **non-synthetic bottle front photos** paired with COLA metadata. Authoritative label artwork comes from the [Texas Open Data Portal — Approved Product Label Search](https://data.texas.gov/dataset/Approved-Product-Label-Search/2cjh-3vae/data_preview):

- Each row’s **File Link** is the approved label image/PDF for that row’s **TTB Number** (exact 1:1 match).
- Fixture meta includes `tabcApprovedLabelFileLink`, `approvedLabelSearchUrl`, and `tabcFileLinkMatchesTtbNumberExactly`.
- `userPhotoMatchesTabcRow` distinguishes when the user’s bottle photo is the same product as the ODP row vs. when only the TTB ID / File Link pairing applies.

```bash
pnpm install:tabc-real-bottles
```

Requires the Texas ODP CSV export (default: `%USERPROFILE%\Downloads\Approved_Product_Label_Search_20260614.csv`). Override with `TABC_CSV_PATH`.

These fixtures use `labelPresentation: realBottleFrontWithWarningCheck`: visible identity fields should pass OCR, but **overall verification fails** because front-label photos lack the government warning text and bold phrase.

### 5. Do not scrape licensed commercial photography

Do not copy retailer, brand, or stock product photos without a clear PD/CC0 license. When in doubt, use the synthetic generator or a manual registry printable.

## Regenerating fixtures

```bash
pnpm fetch:colas:photos
pnpm setup:reviewer-pack
```

To force synthetic images, delete `{ttbId}.png` and run `pnpm fetch:colas`. To keep a manual registry image, leave the PNG in place before running the script.
