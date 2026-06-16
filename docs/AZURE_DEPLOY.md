# Azure deployment — Label Verification

Deploy the single-container app (Next.js static + .NET API + Tesseract) to **Azure App Service for Linux containers** on **P2v3** (4 vCPUs — OCR CPU headroom for 3-page ODP stacks).

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows)
- Docker Desktop
- **Compute quota:** subscription must allow at least **1 VM** in the target region (App Service Linux uses this quota). Free/trial subscriptions often show `Total VMs: 0` until you upgrade to Pay-As-You-Go or request a quota increase.
- Demo passwords in environment (not committed):

```powershell
$env:DEMO_AGENT_PASSWORD = "<from submission packet>"
$env:DEMO_PARALLEL_PASSWORD = "<optional batch demo account>"
```

## Troubleshooting

### `Total VMs: 0` quota error

App Service (F1/B1/B2/P1v3) requires VM quota. In [Azure Portal](https://portal.azure.com) → **Subscriptions** → **Usage + quotas** → search **Total Regional vCPUs** or **App Service plan** → **Request increase** (minimum 1).

Or upgrade **Azure subscription 1** from free trial to Pay-As-You-Go, then retry:

```powershell
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg -AppName label-verify-trackc -AppServiceSku B2
```

P2v3 is recommended for OCR throughput (4 dedicated vCPUs); P1v3 works for lighter demos; B2 for limited subscriptions.

## OCR performance (5s flat-label SLA)

Large TABC flat labels use a **hybrid pipeline**:

1. **OpenCV field-band detector** (`FlatArtworkFieldBandDetector`) — ink-projection locates header, contents, and footer bands so Tesseract runs on ROIs instead of repeating full-page passes.
2. **Composite-bottom passes** on the full stack capture the TTB warning footer without OCR-ing certificate pages.
3. **Single Tesseract engine** (serial) — parallel engine pools corrupt OCR on Windows/Linux with the native Tesseract binding.
4. **P2v3 App Service** — 4 dedicated vCPUs; P1v3 (2 vCPUs) is minimum for reviewer demos; local Windows dev uses 1 OCR engine and runs slower.

Enable targeted OCR in production via `Ocr__UseFieldBandTargetedOcr=true` (set by `azure-deploy.ps1`).

Validate OCR SLA on P2v3 (8s ODP / 5s single-page target):

```powershell
$env:OCR_SLA_TARGET_MS = "5000"
dotnet test backend/LabelVerification.Tests --filter FlatArtworkPerformanceTests
```

### Resource already partially created

If deploy failed mid-way, these may exist in `label-verify-rg`:

- Resource group
- Container registry (`labelverify*.azurecr.io`)

Re-run the deploy script after quota is fixed; it is idempotent for existing RG/ACR.

## One-command deploy

```powershell
cd treasury-label-verification-plan
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg -Location eastus -AppName label-verify-trackc
```

This script:

1. Creates resource group, ACR, P2v3 plan (falls back to P1v3/B2), Linux Web App
2. Copies tessdata, builds `Dockerfile`, pushes to ACR
3. Sets `WEBSITES_PORT=8082`, demo seed, OCR paths, HTTPS-only

## Post-deploy verification

```powershell
.\scripts\smoke-production.ps1 -BaseUrl https://label-verify-trackc.azurewebsites.net
.\scripts\run-benchmarks.ps1 -BaseUrl https://label-verify-trackc.azurewebsites.net
```

## App settings reference

| Setting | Value |
|---------|--------|
| `WEBSITES_PORT` | `8082` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8082` |
| `Ocr__TessDataPath` | `/app/tessdata` |
| `Ocr__TimeoutSeconds` | `12` (per-label verify budget; flat stacks use field-band OCR) |
| `Ocr__PerLabelWallClockMs` | `8000` (ODP stacks; P2v3 CPU headroom) |
| `Ocr__FlatArtworkMaxOcrSide` | `1200` |
| `Ocr__UseFieldBandTargetedOcr` | `true` |
| `Ocr__FlatArtworkEnginePoolSize` | `6` |
| `Ocr__MaxParallel` | `6` |
| `SEED_DEMO_USERS` | `true` |
| `DISABLE_PUBLIC_REGISTRATION` | `true` |
| `DEMO_AGENT_PASSWORD` | secret |
| `SendGrid__PublicBaseUrl` | `https://<appname>.azurewebsites.net` |

## Persistent auth database

For production demos, mount Azure Files to `/data` and set `AUTH_DB_PATH=/data/auth.db`. The deploy script uses ephemeral container storage by default; re-seed on cold start is acceptable for the take-home POC.

## GitHub Actions (optional)

Add repository secrets `AZURE_CREDENTIALS`, `DEMO_AGENT_PASSWORD`, then run workflow `azure-deploy.yml` on `main`.
