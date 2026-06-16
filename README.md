# Treasury Label Verification

Alcohol beverage label verification portal for the [Treasury take-home assessment](https://github.com/treasurytakehome-rgb/instructions). Agents upload label artwork (PNG/JPEG), provide treasury application values (via CSV manifest or manual entry), and receive field-level pass/fail/review outcomes with OCR confidence bands and agent guidance.

**Live demo:** https://label-verify-trackc.azurewebsites.net  
**Login:** `demo.agent@label-verify.demo` (password in submission packet)

---

## Highlights

| Area | Implementation |
|------|----------------|
| OCR | Multi-pass Tesseract ensemble, flat vs photo classification, Texas ODP 3-page stack handling |
| Verification | Fuzzy field matchers, flat-label compliance (warning placement/contrast/typography), visual warning confirmation |
| UX | CSV manifest intake, reviewer sample pack, sequential full-resolution verify, inline confidence commentary |
| Backend | .NET 8 minimal API, ASP.NET Identity, rate limits, security headers |
| Frontend | Next.js 15 static export served from API `wwwroot` |
| Deploy | Azure App Service P2v3 + ACR, strict production probe gate (5/5 reviewer walkthrough) |

---

## Quick start (local)

### Prerequisites

- Node 20+, pnpm 9+
- .NET 8 SDK
- Tesseract `eng.traineddata` (copied on `pnpm install` via postinstall script)

### 1. Install and sample data

```powershell
cd treasury-label-verification-plan
pnpm install
pnpm setup:reviewer-pack
```

### 2. API (port 8082)

```powershell
cd backend
$env:SEED_DEMO_USERS = "true"
$env:DEMO_AGENT_PASSWORD = "<choose a local dev password>"
$env:Ocr__TessDataPath = "..\tessdata"
$env:ASPNETCORE_URLS = "http://localhost:8082"
dotnet run --project LabelVerification.Api --no-launch-profile
```

### 3. UI dev server (port 3002)

```powershell
cd treasury-label-verification-plan
pnpm dev
```

Open http://localhost:3002 — sign in with `demo.agent@label-verify.demo` and the password you set in `DEMO_AGENT_PASSWORD`.

### Docker (single container, port 8082)

```powershell
docker compose up --build
```

Open http://localhost:8082

---

## Reviewer walkthrough (2 minutes)

1. **Login** at `/login`
2. **Load samples** on the Verify page (five Texas ODP reviewer pack labels)
3. **Verify labels (5)** — standard mode; ~30s per ODP flat label
4. **Expected:** samples 1–2 **fail** (bottle photo ≠ approved product), samples 3–5 **pass**

Full steps: [docs/REVIEWER_WALKTHROUGH.md](docs/REVIEWER_WALKTHROUGH.md)

---

## CSV manifest workflow

1. Download the template from **Guidelines** or [`public/templates/label-verification-manifest.csv`](public/templates/label-verification-manifest.csv)
2. One row per label: `labelImage`, `brandName`, `fancifulName`, `classTypeDesignation`, `abvPercent`, `netContents`, `bottlerProducerAddress`, `countryOfOrigin`, `productCategory`, `labelPresentation`, `ttbWarningText`, `boldWarningPhrase`
3. On Verify, click **CSV manifest** — parameters load, then pick matching PNG/JPEG files by filename
4. Click **Verify labels**

Parser: [`src/lib/labelVerificationCsv.ts`](src/lib/labelVerificationCsv.ts)

---

## Azure deployment

```powershell
az login
$env:DEMO_AGENT_PASSWORD = "<submission packet>"
cd treasury-label-verification-plan
.\scripts\azure-deploy.ps1 -ResourceGroup label-verify-rg -UseAcrBuild
```

Post-deploy strict gate runs `scripts/_probe_production.py --strict` (5/5 single + UI sequential paths).

Details: [docs/AZURE_DEPLOY.md](docs/AZURE_DEPLOY.md)

---

## Tests and probes

```powershell
# Backend unit/integration tests
cd backend
$env:Ocr__TessDataPath = "..\tessdata"
dotnet test

# Production reviewer gate (requires DEMO_AGENT_PASSWORD in environment)
$env:DEMO_AGENT_PASSWORD = "<submission packet password>"
python scripts/_probe_production.py --strict

# Quick smoke (health + login)
$env:DEMO_AGENT_PASSWORD = "<submission packet password>"
.\scripts\smoke-production.ps1 -BaseUrl https://label-verify-trackc.azurewebsites.net
```

Benchmarks: [docs/BENCHMARKS.md](docs/BENCHMARKS.md)

---

## Project layout

```
treasury-label-verification-plan/
├── backend/                 # .NET 8 API + OCR + verification core
├── src/                     # Next.js 15 UI (static export → wwwroot)
├── public/                  # Static assets, samples, CSV template
├── scripts/                 # Deploy, probes, reviewer pack, fixtures
├── docs/                    # Architecture, security, walkthrough, benchmarks
├── e2e/                     # Playwright smoke tests
├── testdata/                # Fixtures and layout annotations
├── tessdata/                # Tesseract English model (eng.traineddata)
├── Dockerfile               # Multi-stage API + frontend build
├── docker-compose.yml
└── README.md                # This file
```

**Every file described:** [docs/FILE_REFERENCE.md](docs/FILE_REFERENCE.md)

---

## Documentation index

| Document | Purpose |
|----------|---------|
| [docs/APPROACH.md](docs/APPROACH.md) | Product and OCR strategy |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System components and data flow |
| [docs/REVIEWER_WALKTHROUGH.md](docs/REVIEWER_WALKTHROUGH.md) | 2-minute demo script |
| [docs/BENCHMARKS.md](docs/BENCHMARKS.md) | Test and production outcomes |
| [docs/AZURE_DEPLOY.md](docs/AZURE_DEPLOY.md) | App Service + ACR deploy |
| [docs/SECURITY.md](docs/SECURITY.md) | Security controls (technical) |
| [SECURITY.md](SECURITY.md) | Vulnerability reporting policy |
| [docs/FILE_REFERENCE.md](docs/FILE_REFERENCE.md) | Per-file descriptions |
| [docs/COLA_INTEGRATION.md](docs/COLA_INTEGRATION.md) | Demo COLA cache (optional) |
| [BUILD_STATUS.md](BUILD_STATUS.md) | Current build and probe status |

In-app pages: **Guidelines** (`/guidelines`), **Security** (`/security`)

---

## API overview

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/auth/login` | Cookie session login |
| POST | `/api/v1/verify` | Single-label verify (multipart: `image`, `expected` JSON) |
| POST | `/api/v1/verify/batch` | Batch verify (API clients; UI uses sequential singles) |
| GET | `/health/live` | Liveness |
| GET | `/health/ready` | Readiness (OCR warmed) |

Authenticated **Approved** users only for verify routes.

---

## Stack

- **Backend:** .NET 8, Tesseract OCR, OpenCV preprocessing, ASP.NET Identity (SQLite demo)
- **Frontend:** Next.js 15, React 19, Tailwind CSS, static export
- **Infra:** Azure App Service Linux P2v3, Azure Container Registry, optional SendGrid for contact/approval email

---

## License and attribution

Take-home assessment submission. Treasury branding follows demo guidelines in [docs/BRANDING.md](docs/BRANDING.md).
