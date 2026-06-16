# Security

Technical security documentation for the Treasury Label Verification Tool (Label Verification Portal). For vulnerability reporting, see [SECURITY.md](../SECURITY.md) at the repository root. For an in-app summary, open **Security** in the portal navigation (`/security`).

---

## Authentication and authorization

- **Cookie-based sessions** via ASP.NET Identity (`HttpOnly`, `SameSite=Lax`).
- In **Production**, auth cookies use `SecurePolicy.Always` (HTTPS-only).
- **Verify**, **contact**, and related endpoints require an authenticated user with **Approved** status.
- **Registration** creates `Pending` users until an approver completes the email approval flow.

### Registration approval

- Approver email links open `/approve?token=…` (query token acceptable for demo navigation).
- The approval **action** is submitted via **POST** `/api/v1/auth/approve` with `{ token, action }`.
- **GET** `/api/v1/auth/approve` is disabled in production (`Auth:AllowGetApprovalAction=false`).
- Approval tokens expire after **1 hour** by default (`Auth:ApprovalTokenTtlHours`).

---

## Transport and browser hardening

- **Security headers middleware:** Content-Security-Policy, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`.
- **HSTS** emitted in Production when the request is HTTPS.
- Azure deploy script sets **HTTPS-only** on the App Service.
- **Kestrel** disables minimum response data rate limits so long sequential verify sessions are not dropped mid-OCR.

---

## Abuse controls

| Control | Detail |
|---------|--------|
| Rate limiting | Fixed window on auth, contact, and verify route groups |
| Upload validation | PNG/JPEG only; configurable max bytes (default **5 MB** per image) |
| OCR gates | Global `OCR_MAX_PARALLEL` and per-user `OCR_MAX_PER_USER` semaphores |
| Public registration | `DISABLE_PUBLIC_REGISTRATION=true` on production demo |

---

## Data handling

- **Label images** are processed in memory for OCR; the demo app does not persist uploaded artwork to disk.
- **Verification history** in the UI is stored in **browser localStorage** only (client-side audit trail).
- **Raw OCR text** is returned in API responses for reviewer transparency; restrict API access in production.
- **CSV manifests** are parsed client-side; only images and expected JSON are sent to the server on verify.

---

## Secrets management

| Secret | Storage |
|--------|---------|
| `SendGrid__ApiKey` | Environment variable / Azure App Settings / Key Vault — **never committed** |
| `DEMO_AGENT_PASSWORD` | Environment variable; seeded on cold start when `SEED_DEMO_USERS=true` |
| `DEMO_PARALLEL_PASSWORD` | Optional second demo account password |
| ACR credentials | Retrieved at deploy time via `az acr credential show` |

Rotate SendGrid keys and demo credentials after public demos.

---

## Identity persistence (demo vs production)

- **Demo:** Ephemeral **SQLite** (`auth.db`) on App Service. Cold start re-seeds demo users when `SEED_DEMO_USERS=true`.
- **Production recommendation:** Azure SQL Database, managed identity, no demo seeding, centralized audit logging.

---

## Dependency and supply chain

- **Container images** built in Azure Container Registry from pinned Dockerfile base tags (`DOTNET_SDK_IMAGE`, `DOTNET_RUNTIME_IMAGE` args).
- **Frontend** dependencies locked in `pnpm-lock.yaml`; CI runs on GitHub Actions.
- **Tesseract** `eng.traineddata` loaded from `/app/tessdata` in container; downloaded at deploy if missing locally.

---

## Out of scope (this demo)

- Web Application Firewall (WAF) rules and bot management
- CAPTCHA on login or verify
- Structured immutable audit log to SIEM
- Live COLA / TTB system mutual TLS
- HIPAA or PCI processing (no payment data; no PHI workflow)

---

## Secure deployment checklist

1. Set strong `DEMO_AGENT_PASSWORD`; disable public registration.
2. Configure SendGrid via App Settings or Key Vault.
3. Enable HTTPS-only on App Service (automated in `azure-deploy.ps1`).
4. Do not commit `.env.local`, `auth.db`, or API keys.
5. Run `python scripts/_probe_production.py --strict` after deploy to confirm health.
6. Set Azure cost budget alerts (`scripts/setup-azure-budget.ps1` or Portal).

---

## Related documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — OCR concurrency and API flow
- [AZURE_DEPLOY.md](AZURE_DEPLOY.md) — Production environment variables
- [FILE_REFERENCE.md](FILE_REFERENCE.md) — Security-related source files
