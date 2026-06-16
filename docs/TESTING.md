# Testing

## Primary verification path

Production verification compares **label image OCR** to **agent-entered treasury application values** via `POST /api/v1/verify` (or batch). Tests that matter most:

| Suite | What it proves |
|-------|----------------|
| `VerifyEndpointTests` | HTTP multipart accepts camelCase enums; submitted expected values are used (not COLA cache) |
| `ReviewerPackMismatchTests` | Mismatch bottle photos **fail** against application metadata |
| `ReviewerPackSampleTests` | Texas ODP artwork **passes** against manifest expected values (Linux CI) |
| `DecisionFrameworkTests` / `VerificationFixtureTests` | Matcher and readability logic |

**Demo COLA mode** (`AutonomousVerificationTests`, autonomous API) is optional regression for approved-label cache — not the production workflow.

## Platform notes

| Environment | OCR engines | ODP stack tests |
|-------------|-------------|-----------------|
| **Linux CI** (ubuntu-latest) | 6-engine pool | Full reviewer pack (Ambhar, Venenosa, Jack) |
| **Windows dev** | 1 engine | Jack ODP only; Ambhar/Venenosa skipped (single-engine timeout/quality) |

Run locally:

```powershell
dotnet test backend/LabelVerification.Tests/LabelVerification.Tests.csproj --configuration Release
pnpm build
pnpm run test:e2e   # Playwright: login → load samples → standard verify Jack
python scripts/_probe_production.py   # STD lines = production path
```

## Production probe

`_probe_production.py` exercises **STD** (standard verify with manifest expected JSON) and **AUTO** (demo COLA). Judge production readiness on **STD** results.
