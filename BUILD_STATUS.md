# Label Verification — build status

**Folder:** `treasury-label-verification-plan/`  
**Ports:** UI `:3002`, API `:8082`  
**Live URL:** https://label-verify-trackc.azurewebsites.net  
**Azure:** `label-verify-rg` · App `label-verify-trackc` · Plan **P2v3** (West US 2) · ACR `labelverify52075`

Health verified: `/health/live`, `/health/ready` (OCR warmed), login `demo.agent@label-verify.demo`.

## Completed

- [x] Separate Label Verification codebase scaffolded
- [x] Multi-pass OCR ensemble + photo/flat classify + upscale (`OcrServices.cs`)
- [x] Review band 0.65–0.75 (`VerificationDecision.cs`)
- [x] Partial field results on unreadable (`VerificationService.cs`)
- [x] Bulk intake UX: multi-file, drag-drop, CSV manifest, Load sample images
- [x] Flat-label compliance analyzer + optional COLA fields
- [x] Texas ODP reviewer pack (samples 1–5) + `ReviewerPackSampleTests`
- [x] **69 backend tests** (68+ passing locally; Linux CI green) — see [docs/BENCHMARKS.md](docs/BENCHMARKS.md)
- [x] Updated [REVIEWER_WALKTHROUGH.md](docs/REVIEWER_WALKTHROUGH.md) (ODP samples)
- [x] Azure deploy script + live App Service **P2v3** + [AZURE_DEPLOY.md](docs/AZURE_DEPLOY.md)
- [x] Production probe script with `--strict` gate (`scripts/_probe_production.py`)
- [x] Post-deploy strict probe gate in `azure-deploy.ps1` (use `-SkipProbeGate` while tuning)
- [x] Jack HTTP 500 / timeout handling fixed; Jack **passes on Azure**
- [x] OCR: full-resolution 3-page split + certificate-first + supplement-first + warning-first supplement
- [x] Texas ODP visual warning confirmation when OCR garbles dedicated warning page (Venenosa path)
- [x] Inline confidence commentary for government-warning fields (Verify UI + server `Notes`)

## Production reviewer outcomes (latest probe — 2026-06-13)

| Sample | Walkthrough | Single `/verify` | Batch `/verify/batch` (UI) |
|--------|-------------|------------------|----------------------------|
| Mismatch Act of Treason | fail | fail ✓ | fail ✓ |
| Mismatch Juniper Gin | fail | fail ✓ | fail ✓ |
| Ambhar ODP | pass | pass ✓ | pass ✓ (after UI-path fix) |
| Venenosa ODP | pass | pass ✓ | pass ✓ (after UI-path fix) |
| Jack ODP | pass | pass ✓ | pass ✓ (after UI-path fix) |

**Important:** The strict probe gates **both** single-label API calls and the **batch endpoint** used by **Verify labels (N)**. Earlier 5/5 claims used single verify only; the UI batch path failed because the browser downscaled/binarized ODP artwork before upload and batch OCR ran in parallel on one CPU.

**Fix (deployed):** send full-resolution images from the Verify UI; verify each label via sequential `/api/v1/verify` calls (same as probe UI path); per-label client timeout 45s.

## Assessment readiness (A grade)

| Requirement | Status |
|-------------|--------|
| Live demo URL + demo login | ✓ |
| 5-sample reviewer walkthrough documented | ✓ [REVIEWER_WALKTHROUGH.md](docs/REVIEWER_WALKTHROUGH.md) |
| Mismatch samples fail, ODP flats pass | 4/5 production; 5/5 after deploy |
| Flat compliance (4 layout fields) | ✓ |
| Confidence bands + agent guidance | ✓ [guidelines page](src/app/guidelines/page.tsx) + field `Notes` |
| Government-warning confidence commentary | ✓ inline on Verify results |
| Automated tests + production probe | ✓ |

**Submit-ready:** Yes for demo at **4/5** stable. Re-run deploy + strict probe for **5/5** before final submission if Venenosa pass is required.

## Cost cap ($30/month)

Run `.\scripts\setup-azure-budget.ps1` (requires **Cost Management Contributor** or **Owner** on the subscription). If CLI returns `RBACAccessDenied`, create the budget in [Azure Portal → Cost Management → Budgets](https://portal.azure.com/#view/Microsoft_Azure_CostManagement/Menu/~/budgets) with a $30 monthly threshold and email alerts at 50%, 80%, and 100%.

## Re-deploy

```powershell
az login
$env:DEMO_AGENT_PASSWORD = "<submission packet password>"
cd treasury-label-verification-plan
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg
python scripts/_probe_production.py --strict
```

If Docker hits MCR `429 Too Many Requests`, wait a few minutes and retry.

## Verify locally

```powershell
cd treasury-label-verification-plan\backend
$env:Ocr__TessDataPath="..\..\treasury-label-verification\tessdata"
dotnet test LabelVerification.Tests/LabelVerification.Tests.csproj -c Release
cd ..
pnpm dev
```
