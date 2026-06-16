# Verification benchmarks — Label Verification

Last verified: **2026-06-15** (Release build + Azure production probe)

## Automated test suite

```
Total tests: 70 (68 excluding Windows-only SLA perf tests)
Passed: 68–70 (Windows dev: ~68/70; Linux CI: 70/70)
Failed: 0–2 (intermittent Ambhar OCR diagnostic on Windows)
Duration: ~3 min (includes ODP flat-label OCR runs)
```

Regenerate locally:

```powershell
cd treasury-label-verification-plan\backend
$env:Ocr__TessDataPath = "..\..\treasury-label-verification\tessdata"
dotnet test LabelVerification.Tests/LabelVerification.Tests.csproj -c Release
```

## Pass-rate summary

| Category | Samples | Pass rate | Notes |
|----------|--------:|----------:|-------|
| Synthetic fixtures (`VerificationFixtureTests`) | 12 | **100%** | Each completes **< 5000 ms** perf gate |
| ODP approved flat labels (`ReviewerPackSampleTests`) | 3 | **100%** on Linux CI | Windows dev skips Ambhar/Venenosa (sequential OCR) |
| Mismatch bottle photos (demo manifest) | 2 | **100%** fail-as-expected | Catches wrong product on photo |
| Unreadable glare fixtures | 2 | **100%** unreadable | Agent guidance returned |
| Flat compliance (`FlatLabelComplianceTests`) | 3 ODP | **100%** | 4 layout/format fields per sample |
| OCR diagnostics (Jack/Ambhar/Venenosa) | 4 | **100%** Jack/Venenosa text; Ambhar brand | Cert digits still sparse on Windows |
| Decision framework unit tests | 4 | **100%** | Pass / fail / review bands |
| Auth integration | 1 | **100%** | Protected verify endpoint |

## Production reviewer probe (Azure P2v3)

Script: `python scripts/_probe_production.py --strict`  
URL: https://label-verify-trackc.azurewebsites.net  
OCR budget: **15 s** wall clock, **6** parallel engines, **15 s** request timeout

| # | File | Expected | Actual (2026-06-15) | Wall time | Notes |
|---|------|----------|---------------------|-----------|-------|
| 1 | `01-mismatch-act-of-treason.png` | fail | **fail** ✓ | ~4 s | Mismatch fields detected |
| 2 | `02-mismatch-juniper-tree-gin.png` | fail | **fail** ✓ | ~6 s | Country + warning mismatch |
| 3 | `03-odp-ambhar-plata.png` | pass | **pass** ✓ | ~30 s | Cert inferrals + supplement-first |
| 4 | `04-odp-la-venenosa-raicilla.png` | pass | **fail** ✗ | ~30 s | Warning fields only (fix deployed pending) |
| 5 | `05-odp-jack-daniels-old-no7.png` | pass | **pass** ✓ | ~32 s | All fields + compliance |

**Reviewer STD outcomes: 4/5** on current Azure (stable). Local build with visual warning confirmation passes Venenosa (`VenenosaVerificationTests`).

## Performance (typical)

| Input type | Target | Observed (Azure P2v3) |
|------------|--------|------------------------|
| Synthetic flat fixture (900×1200) | < 5 s | ~2–4 s |
| Single bottle photo | < 5 s | ~3–6 s |
| Stacked ODP flat artwork (3-page) | Submission-grade | ~17–21 s (full OCR + compliance) |
| Batch of 5 reviewer samples | Interactive | ~2 min total |

Stacked ODP flats exceed Sarah’s 5 s single-label target by design when running full submission-grade OCR and compliance; synthetic fixtures and bottle photos meet the perf gate used in CI.

## OCR fixes in this release

- **Full-resolution page split** — 3-page Texas ODP stacks no longer collapse to 2 pages after downscale (fixes brand ROI for Ambhar/Venenosa).
- **Certificate-first passes** — upscaled cert band (SingleBlock + SparseText) before parallel label crops.
- **Supplement-first + warning-first** — cert/warning supplement runs before main OCR; warning page crops prioritized when statutory text is garbled.
- **Texas ODP visual warning confirmation** — when dedicated warning-page contrast and footer density pass but OCR garbles text, warning fields reconcile with 82% confidence and agent-facing notes.
- **Government-warning confidence commentary** — failed warning fields include band (green/yellow/red) and review guidance in API `Notes` and Verify UI inline text.
- **Label deep passes always run** for multi-page stacks even when cert metadata satisfies corpus sufficiency (preserves Jack class-type matching).
- **Cert page field bands** — upscaled cert page (1600 px min) with header/contents crops + certificate-aware ABV/net matcher fallbacks.

## Regenerate production probe

```powershell
$env:DEMO_AGENT_PASSWORD = "<submission packet password>"
python scripts/_probe_production.py --strict
```

Deploy with optional strict gate (120 s warm-up):

```powershell
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg
# Iterative deploys while tuning OCR:
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg -SkipProbeGate
```
