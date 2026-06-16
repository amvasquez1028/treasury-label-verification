"use client";

import { useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { TreasuryLayout } from "@/components/TreasuryLayout";

type ApprovalOutcome = "idle" | "loading" | "success" | "error";

const postApproval = async (token: string, action: "approve" | "deny") => {
  const response = await fetch("/api/v1/auth/approve", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ token, action }),
  });

  const payload = (await response.json().catch(() => ({}))) as { message?: string };
  if (!response.ok) {
    throw new Error(payload.message ?? `Approval failed (${response.status})`);
  }

  return payload.message ?? (action === "approve" ? "User approved." : "User denied.");
};

export default function ApprovePageClient() {
  const searchParams = useSearchParams();
  const token = useMemo(() => searchParams.get("token")?.trim() ?? "", [searchParams]);
  const [outcome, setOutcome] = useState<ApprovalOutcome>("idle");
  const [message, setMessage] = useState<string | null>(null);

  const handleAction = async (action: "approve" | "deny") => {
    if (!token) {
      setOutcome("error");
      setMessage("Missing approval token in the link.");
      return;
    }

    setOutcome("loading");
    setMessage(null);

    try {
      const resultMessage = await postApproval(token, action);
      setOutcome("success");
      setMessage(resultMessage);
    } catch (error) {
      setOutcome("error");
      setMessage(error instanceof Error ? error.message : "Approval request failed.");
    }
  };

  return (
    <TreasuryLayout title="Registration approval" showNav={false}>
      <div className="mx-auto max-w-md px-4 pb-10">
        <section className="parameter-card space-y-4">
          <p className="text-sm text-[var(--color-base)]">
            Confirm or deny the pending registration request. This action uses a short-lived token and
            submits via POST (tokens are not accepted over GET in production).
          </p>

          {!token ? (
            <p className="rounded border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
              Invalid approval link — no token was provided.
            </p>
          ) : null}

          {message ? (
            <p
              className={`rounded border px-3 py-2 text-sm ${
                outcome === "success"
                  ? "border-green-300 bg-green-50 text-green-900"
                  : "border-red-300 bg-red-50 text-red-800"
              }`}
              role="status"
            >
              {message}
            </p>
          ) : null}

          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              disabled={!token || outcome === "loading" || outcome === "success"}
              onClick={() => void handleAction("approve")}
              className="rounded bg-[var(--color-primary-darker)] px-4 py-2 text-sm font-semibold text-white hover:bg-[var(--color-primary-dark)] disabled:opacity-60"
              aria-label="Approve registration"
            >
              {outcome === "loading" ? "Submitting..." : "Approve"}
            </button>
            <button
              type="button"
              disabled={!token || outcome === "loading" || outcome === "success"}
              onClick={() => void handleAction("deny")}
              className="rounded border border-[var(--color-base-lighter)] px-4 py-2 text-sm font-semibold hover:bg-[var(--color-base-lighter)] disabled:opacity-60"
              aria-label="Deny registration"
            >
              Deny
            </button>
          </div>
        </section>
      </div>
    </TreasuryLayout>
  );
}
