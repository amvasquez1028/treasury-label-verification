# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| `main` / `master` (Treasury Label Verification Tool) | Yes |
| Earlier take-home tracks | No |

## Reporting a vulnerability

This is a **demonstration application** for a take-home assessment. For production Treasury systems, follow your organization's disclosure process.

For this repository:

1. **Do not** open public GitHub issues for exploitable vulnerabilities.
2. Email the repository owner with a description, reproduction steps, and impact assessment.
3. Allow reasonable time for a fix before public disclosure.

## Scope

In scope: authentication bypass, authorization flaws, injection, unsafe file upload handling, secret exposure in the repo, and misconfiguration documented in [docs/SECURITY.md](docs/SECURITY.md).

Out of scope: denial-of-service against the public demo URL, social engineering, and issues in third-party Azure/Tesseract/Next.js without a demonstrable impact on this app.

## Secure deployment checklist

- Set strong `DEMO_AGENT_PASSWORD` and rotate after demos.
- Enable `DISABLE_PUBLIC_REGISTRATION=true` on public hosts.
- Store `SendGrid__ApiKey` in Azure Key Vault or App Settings (never in git).
- Keep HTTPS-only enabled on App Service.
- Do not commit `.env.local`, `auth.db`, or API keys.

See [docs/SECURITY.md](docs/SECURITY.md) for architecture-level controls.
