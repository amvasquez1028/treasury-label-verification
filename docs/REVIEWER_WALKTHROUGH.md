# Reviewer walkthrough — Label Verification (2 minutes)

**Live demo:** https://label-verify-trackc.azurewebsites.net (after Azure deploy)  
**Local:** http://localhost:3002

Demo account: `demo.agent@label-verify.demo` (password provided separately — 12+ chars with a digit).

## Steps

1. **Login** at `/login`
2. **Load samples** — on Verify page, click **Load sample images** (5 files from Texas ODP reviewer pack). Each row is prefilled with **application/treasury expected values** from the manifest (not from live COLA).
3. **Verify** — click **Verify labels (5)** (standard mode, not Demo COLA mode). The UI verifies **one label at a time** at full resolution (~30s per Texas ODP flat; ~2.5 minutes total for five). Single-page bottle photos are faster (~5–8s).
4. **Expected outcomes**

| # | File | Expected | Why |
|---|------|----------|-----|
| 1 | `01-mismatch-act-of-treason.png` | **Fail** | Real bottle photo ≠ Texas ODP approved product for that TTB row |
| 2 | `02-mismatch-juniper-tree-gin.png` | **Fail** | Same — photo/product mismatch |
| 3 | `03-odp-ambhar-plata.png` | **Pass** | Texas ODP flat label artwork (TTB 14106001000237) |
| 4 | `04-odp-la-venenosa-raicilla.png` | **Pass** | Texas ODP flat label artwork (TTB 14086001000323) |
| 5 | `05-odp-jack-daniels-old-no7.png` | **Pass** | Texas ODP flat label artwork (TTB 13343001000271) |

5. **Flat compliance** — samples 3–5 include four layout checks when `labelPresentation` is **Full label**: warning placement, warning contrast, bold typography, label text contrast
6. **Batch** — use **Choose label images** or drag-drop multiple files; optional CSV manifest (`filename,ttbId`) for pairing only
7. **Demo COLA mode** (optional) — toggles autonomous verify against the local approved-label cache; for testing only, not required for reviewer walkthrough
8. **Unreadable** — upload `testdata/fixtures/unreadable-glare-01.png` (or heavy glare photo) → **Unreadable** with agent guidance and partial field extraction

## Sample TTB IDs (reviewer pack)

| TTB ID | Product |
|--------|---------|
| `14106001000237` | Ambhar Plata Tequila |
| `14086001000323` | La Venenosa Raicilla |
| `13343001000271` | Jack Daniel's Old No. 7 (375 mL) |
| `21194001000323` | Act of Treason (mismatch demo) |
| `18055001000023` | The Juniper Tree Gin (mismatch demo) |

## Texas ODP source

Approved label artwork is extracted from [Texas Approved Product Label Search](https://data.texas.gov/dataset/Approved-Product-Label-Search/2cjh-3vae) **File Link** PDFs (embedded JPEG pages, not certificate-only page 0).

Regenerate local samples:

```powershell
pnpm setup:reviewer-pack
```

## Smoke test (production URL)

```powershell
$env:DEMO_AGENT_PASSWORD = "<provided separately>"
.\scripts\smoke-production.ps1 -BaseUrl https://label-verify-trackc.azurewebsites.net
```
