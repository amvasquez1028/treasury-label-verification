# File reference

Detailed description of each significant file and directory in this repository. Generated for the Treasury Label Verification Tool (`treasury-label-verification-plan`).

---

## Root

| File | Description |
|------|-------------|
| `README.md` | Project overview, quick start, deploy, and documentation index. |
| `SECURITY.md` | GitHub security policy: supported versions and vulnerability reporting. |
| `BUILD_STATUS.md` | Current feature checklist, production probe outcomes, and assessment readiness. |
| `Dockerfile` | Multi-stage build: .NET 8 API publish, Node 20 Next.js static export, runtime image with Tesseract + tessdata. |
| `docker-compose.yml` | Local single-container run on port 8082 with OCR env defaults. |
| `.dockerignore` | Excludes node_modules, .git, and build artifacts from Docker context. |
| `.gitignore` | Ignores bin/obj, .next, secrets, logs, and ephemeral SQLite. |
| `.env.example` | Template env vars (OCR parallelism, demo passwords) — copy to `.env.local` for dev. |
| `package.json` | Node scripts: dev, build, reviewer pack setup, fixture generators, Playwright e2e. |
| `pnpm-lock.yaml` | Locked frontend dependencies. |
| `next.config.ts` | Static export, API rewrites for local dev proxy to :8082. |
| `tailwind.config.ts` | Treasury USWDS-inspired color tokens and typography. |
| `tsconfig.json` | TypeScript paths (`@/*` → `src/*`). |
| `eslint.config.mjs` | Next.js ESLint flat config. |
| `playwright.config.ts` | E2E test runner against local or deployed base URL. |

---

## `backend/` — .NET 8 API

### `LabelVerification.Api/`

| File | Description |
|------|-------------|
| `Program.cs` | App bootstrap: DI, rate limits, Kestrel long-batch timeouts, static files, OCR warm-up. |
| `Endpoints/ApiEndpoints.cs` | Minimal API routes: auth, verify, batch, COLA cache prefill, contact, health. |
| `Middleware/SecurityHeadersMiddleware.cs` | CSP, HSTS, X-Frame-Options, nosniff, Referrer-Policy. |
| `Middleware/StaticExportFallbackMiddleware.cs` | SPA fallback for Next.js static export routes. |
| `Services/OcrLimits.cs` | Global OCR concurrency gate and per-user limiter. |
| `appsettings.json` | Default OCR timeouts, layout paths, readability thresholds. |

### `LabelVerification.Core/`

| File | Description |
|------|-------------|
| `Services/VerificationService.cs` | Orchestrates OCR, readability, flat compliance, field matching, batch verify. |
| `Services/VerificationDecision.cs` | Pass / review / fail / unreadable / timeout decision rules. |
| `Ocr/OcrServices.cs` | Multi-pass Tesseract ensemble, flat artwork crops, supplement passes, wall-clock budgets. |
| `Ocr/OcrReadabilityAssessor.cs` | Classifies unreadable images (blur, glare, low contrast). |
| `Ocr/FlatArtworkFieldBandDetector.cs` | Detects horizontal bands on stacked flat label PDF exports. |
| `Matching/FieldMatchers.cs` | Per-field fuzzy matchers (brand, ABV, net contents, address, warnings). |
| `Matching/WarningTextHelper.cs` | TTB warning text normalization and garbled-OCR token relaxations. |
| `Matching/FieldConfidenceCommentary.cs` | Agent-facing notes for failed warning fields (confidence bands). |
| `Compliance/FlatLabelComplianceAnalyzer.cs` | Warning placement, contrast, bold typography, label text contrast. |
| `Compliance/FlatWarningVisualConfirmation.cs` | Visual ink/contrast confirmation when warning OCR is garbled (Venenosa path). |
| `Compliance/StackedTabcPageHelper.cs` | Splits Texas ODP multi-page label stacks for targeted OCR. |
| `Extraction/AutonomousVerificationService.cs` | Demo COLA autonomous extract + cache lookup (API only). |
| `Extraction/LabelFieldExtractor.cs` | Heuristic field extraction from raw OCR text. |
| `Cola/ColaPublicCache.cs` | Local approved-label JSON cache for demo prefill. |
| `Layout/*` | Optional ONNX/heuristic layout ROI detection for guided OCR. |
| `Models/VerificationModels.cs` | DTOs: `VerificationResult`, `FieldVerificationResult`, batch types. |
| `Options/OcrOptions.cs` | Configurable OCR parallelism, wall clocks, flat artwork limits. |

### `LabelVerification.Infrastructure/`

| File | Description |
|------|-------------|
| `InfrastructureExtensions.cs` | ASP.NET Identity, SQLite auth DB, SendGrid email, FluentValidation wiring. |

### `LabelVerification.Tests/`

| File | Description |
|------|-------------|
| `ReviewerPackSampleTests.cs` | Texas ODP samples 3–5 pass/mismatch fail against manifest expected values. |
| `JackDanielsVerificationTests.cs` | Jack Daniels ODP stability and inferral regression tests. |
| `VenenosaVerificationTests.cs` | Venenosa warning-field and visual confirmation tests. |
| `WarningTextHelperTests.cs` | Warning text matcher unit tests. |
| `FlatLabelComplianceTests.cs` | Flat compliance analyzer coverage. |
| `DecisionFrameworkTests.cs` | Pass/review/fail threshold tests. |
| `VerifyEndpointTests.cs` | HTTP integration tests for `/api/v1/verify`. |
| `OcrEngineFixture.cs` | Shared Tesseract fixture for OCR-heavy tests. |
| `LabelVerifyWebFactory.cs` | WebApplicationFactory for API integration tests. |

---

## `src/` — Next.js UI

### `src/app/` — Pages

| File | Description |
|------|-------------|
| `layout.tsx` | Root layout, Public Sans font, global CSS. |
| `page.tsx` | Home / landing with navigation cards. |
| `login/page.tsx` | Agent login form. |
| `verify/page.tsx` | Main workflow: CSV manifest, Load samples, label rows, sequential verify. |
| `history/page.tsx` | Browser localStorage verification history. |
| `guidelines/page.tsx` | Agent guidelines, CSV template link, decision framework tables. |
| `security/page.tsx` | In-app security controls summary for reviewers. |
| `contact/page.tsx` | Authenticated contact form (SendGrid backend). |
| `approve/page.tsx` | Registration approval landing (email token flow). |
| `error.tsx` / `global-error.tsx` | Error boundaries. |

### `src/components/`

| File | Description |
|------|-------------|
| `TreasuryLayout.tsx` | Page shell: header, nav, footer. |
| `SiteNav.tsx` | Primary navigation and sign-out. |
| `SiteFooter.tsx` | Footer with Treasury homepage link. |
| `AuthGuard.tsx` | Redirects unauthenticated users to login. |
| `LabelVerificationFields.tsx` | Expected-value form (brand, ABV, warnings, etc.). |
| `LabelFileInput.tsx` | Accessible per-row image file picker. |
| `FieldResultDetail.tsx` | Expandable field result with confidence commentary. |
| `VerificationStatusBadge.tsx` | Pass / fail / review / unreadable badge. |
| `ConfidenceScore.tsx` | Green / yellow / red confidence display. |
| `UnreadableResultPanel.tsx` | Guidance when OCR cannot read the image. |
| `ProcessingErrorPanel.tsx` | Timeout and server error messaging. |
| `AutoGrowTextarea.tsx` | Auto-expanding textarea for long warning text fields. |

### `src/lib/`

| File | Description |
|------|-------------|
| `api.ts` | Fetch wrappers: login, verify, sequential batch, COLA prefill, contact. |
| `labelVerificationCsv.ts` | RFC4180 CSV parser, column aliases, image filename matching, template path. |
| `parseCsvManifest.ts` | Legacy CSV adapter (re-exports label CSV parser). |
| `sampleManifest.ts` | Loads `public/samples/manifest.json` and fetches sample PNGs. |
| `defaultExpected.ts` | Default and empty `ExpectedLabelFields` templates. |
| `parseColaCache.ts` | Maps COLA cache JSON to form expected values. |
| `clientOcr.ts` | Client-side Tesseract (optional); server upload passes full resolution. |
| `history.ts` | localStorage persistence for verification runs. |
| `imageQuality.ts` | Non-blocking client-side image quality warnings. |
| `formatFieldName.ts` | Human-readable field labels for results UI. |
| `cachedTtbIds.ts` | Legacy list of demo cache TTB IDs (API/testing reference). |

---

## `public/`

| Path | Description |
|------|-------------|
| `samples/manifest.json` | Reviewer pack metadata: expected fields, TTB IDs, pass/fail expectations. |
| `samples/*.png` | Five Texas ODP reviewer images (generated by setup-reviewer-pack). |
| `templates/label-verification-manifest.csv` | Downloadable CSV template with column headers and example row. |
| `tesseract/` | Bundled tesseract.js worker assets for optional client OCR. |

---

## `scripts/`

| File | Description |
|------|-------------|
| `azure-deploy.ps1` | Build/push ACR image, configure App Service P2v3, strict probe gate. |
| `smoke-production.ps1` | Quick curl-based health + login smoke test. |
| `_probe_production.py` | Production reviewer gate: 5/5 STD single + UI sequential paths. |
| `setup-reviewer-pack.mjs` | Downloads Texas ODP PDFs, extracts label PNGs, writes manifest. |
| `run-benchmarks.ps1` | Runs timed verification benchmarks against a base URL. |
| `run-fixture-tests.ps1` | Wrapper for backend fixture test subsets. |
| `copy-tesseract-assets.mjs` | Postinstall: copies tesseract.js assets to `public/tesseract`. |
| `generate-test-labels.mjs` | Synthetic label PNG generator for fixtures. |
| `fetch-cola-fixtures.mjs` | Fetches COLA JSON cache entries for demo prefill. |
| `setup-azure-budget.ps1` | Optional $30/month Azure budget alert setup. |

---

## `docs/`

| File | Description |
|------|-------------|
| `APPROACH.md` | OCR ensemble strategy and design rationale. |
| `ARCHITECTURE.md` | Component diagram, OCR pipeline, concurrency model. |
| `REVIEWER_WALKTHROUGH.md` | Step-by-step 2-minute reviewer demo. |
| `BENCHMARKS.md` | Test counts, fixture pass rates, production probe history. |
| `AZURE_DEPLOY.md` | App Service, ACR, env vars, cost notes. |
| `SECURITY.md` | Technical security controls (auth, headers, uploads, secrets). |
| `COLA_INTEGRATION.md` | Demo COLA cache vs production verify workflow. |
| `TESTING.md` | How to run unit, integration, and e2e tests. |
| `BRANDING.md` | Treasury visual identity notes. |
| `CONTENT_STYLE.md` | UI copy and tone guidelines. |
| `TTB_REFERENCES.md` | External TTB regulatory links. |
| `FILE_REFERENCE.md` | This file. |

---

## `e2e/`

| File | Description |
|------|-------------|
| `verify-autonomous-jack.spec.ts` | Playwright: login, load samples, verify Jack ODP label. |

---

## `testdata/`

| Path | Description |
|------|-------------|
| `fixtures/` | Synthetic and real bottle PNGs + expected JSON for automated tests. |
| `colas/` | Cached approved-label JSON for demo COLA prefill. |
| `layout-annotations/` | Optional layout ROI annotations for guided OCR. |
| `layout-models/` | Optional ONNX layout model (disabled by default in deploy). |

---

## `tessdata/`

| File | Description |
|------|-------------|
| `eng.traineddata` | Tesseract English language model (required for OCR; may be downloaded on first deploy). |

---

## `.github/workflows/`

| File | Description |
|------|-------------|
| `ci.yml` | GitHub Actions: backend tests on push/PR. |
| `azure-deploy.yml` | Optional CI deploy workflow to Azure. |

---

## Generated / not committed

| Path | Description |
|------|-------------|
| `backend/**/bin/`, `obj/` | .NET build output. |
| `backend/LabelVerification.Api/wwwroot/` | Next.js static export copied at Docker build time. |
| `.next/` | Next.js dev/build cache. |
| `node_modules/` | Node dependencies. |
| `backend/**/auth.db` | Ephemeral SQLite identity database. |
