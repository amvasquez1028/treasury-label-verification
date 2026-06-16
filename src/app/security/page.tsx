import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { TreasuryLayout } from "@/components/TreasuryLayout";

const securitySections = [
  {
    title: "Authentication and sessions",
    items: [
      "ASP.NET Identity with cookie-based sessions (HttpOnly, SameSite=Lax).",
      "Production cookies require HTTPS (SecurePolicy.Always).",
      "Verify and contact endpoints require an authenticated user with Approved status.",
      "Registration creates Pending users until an approver completes the email approval flow.",
    ],
  },
  {
    title: "Authorization and registration",
    items: [
      "Approval links open /approve?token=… for demo navigation only.",
      "Approval actions are submitted via POST /api/v1/auth/approve with { token, action }.",
      "GET approval actions are disabled in production (Auth:AllowGetApprovalAction=false).",
      "Approval tokens expire after 1 hour by default (Auth:ApprovalTokenTtlHours).",
      "Set DISABLE_PUBLIC_REGISTRATION=true on public deployments to block open signup.",
    ],
  },
  {
    title: "Transport and browser hardening",
    items: [
      "Security headers middleware: Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy.",
      "HTTP Strict Transport Security (HSTS) in Production over HTTPS.",
      "App Service deploy script enables HTTPS-only on the web app.",
    ],
  },
  {
    title: "Upload and verification abuse controls",
    items: [
      "PNG and JPEG uploads only; configurable max size (default 5 MB per image).",
      "Rate limiting on auth, contact, and verify route groups.",
      "OCR concurrency gates limit parallel OCR per instance and per user.",
      "No client-side binarization before server OCR — full-resolution artwork is sent for verify.",
    ],
  },
  {
    title: "Secrets and data handling",
    items: [
      "SendGrid API key via environment / Key Vault — never committed to source control.",
      "Demo passwords overridden via DEMO_AGENT_PASSWORD and DEMO_PARALLEL_PASSWORD env vars.",
      "Label images are processed in memory for OCR; no persistent storage of uploaded artwork in the demo app.",
      "Verification history in the UI is stored in browser localStorage only (client-side audit trail).",
    ],
  },
  {
    title: "Operational notes (demo vs production)",
    items: [
      "Ephemeral SQLite (auth.db) on App Service: cold start re-seeds demo users when SEED_DEMO_USERS=true.",
      "Not HA — use Azure SQL and managed identity for production identity persistence.",
      "Rotate SendGrid keys and demo credentials after public demos.",
      "WAF, CAPTCHA, structured audit logging, and live COLA mutual TLS are out of scope for this demo.",
    ],
  },
];

export default function SecurityPage() {
  return (
    <AuthGuard>
      <TreasuryLayout
        title="Security"
        subtitle="How this demo application protects agents, uploads, and credentials."
      >
        <div className="mx-auto max-w-6xl space-y-8 px-4 pb-10">
          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">Overview</h3>
            <p className="mt-4 text-[var(--color-base-darkest)]">
              The Label Verification Portal is a demonstration workflow for comparing OCR-extracted label
              text against treasury application values. It is designed for reviewer walkthroughs and local
              OCR — not as a production TTB system. Controls below reflect what is implemented in this
              codebase and Azure deployment.
            </p>
            <p className="mt-3 text-sm text-[var(--color-base-darkest)]">
              Repository policy: see{" "}
              <a
                href="https://github.com/amvasquez1028/treasury-label-verification/blob/master/SECURITY.md"
                target="_blank"
                rel="noopener noreferrer"
                className="font-semibold text-[var(--color-primary-darker)] underline"
              >
                SECURITY.md on GitHub
              </a>
              . Technical detail:{" "}
              <Link
                href="/guidelines/"
                className="font-semibold text-[var(--color-primary-darker)] underline"
              >
                Guidelines
              </Link>
              .
            </p>
          </section>

          {securitySections.map((section) => (
            <section key={section.title} className="parameter-card">
              <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">{section.title}</h3>
              <ul className="mt-4 list-disc space-y-2 pl-5 text-[var(--color-base-darkest)]">
                {section.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ))}

          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">Reporting issues</h3>
            <p className="mt-4 text-[var(--color-base-darkest)]">
              Do not file public GitHub issues for exploitable vulnerabilities. Contact the repository
              owner with reproduction steps and impact. See the supported versions table in the
              repository SECURITY.md file.
            </p>
          </section>
        </div>
      </TreasuryLayout>
    </AuthGuard>
  );
}
