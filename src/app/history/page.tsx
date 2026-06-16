"use client";

import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { ConfidenceScore } from "@/components/ConfidenceScore";
import { VerificationStatusBadge } from "@/components/VerificationStatusBadge";
import { TreasuryLayout } from "@/components/TreasuryLayout";
import {
  formatHistoryTimestamp,
  getVerificationHistory,
  type HistoryEntry,
} from "@/lib/history";

export default function HistoryPage() {
  const [entries, setEntries] = useState<HistoryEntry[]>([]);

  useEffect(() => {
    setEntries(getVerificationHistory());
  }, []);

  return (
    <AuthGuard>
      <TreasuryLayout
        title="Verification History"
        subtitle="Review past verification results stored locally, including confidence scores and field-level reasoning."
      >
        <div className="mx-auto max-w-6xl px-4 pb-10">
          {entries.length === 0 ? (
            <p className="parameter-card text-[var(--color-base-dark)]">
              No verification history yet. Run a verification on the Verify page to see results here.
            </p>
          ) : (
            <div className="parameter-card overflow-x-auto">
              <table className="w-full min-w-[720px] text-left text-sm">
                <thead>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <th className="px-3 py-2 font-bold">Timestamp</th>
                    <th className="px-3 py-2 font-bold">Brand</th>
                    <th className="px-3 py-2 font-bold">Verification Result</th>
                    <th className="px-3 py-2 font-bold">Confidence Score</th>
                    <th className="px-3 py-2 font-bold">Reasoning</th>
                  </tr>
                </thead>
                <tbody>
                  {entries.map((entry) => (
                    <tr key={entry.id} className="border-b border-[var(--color-base-lighter)] align-top">
                      <td className="px-3 py-3 whitespace-nowrap">
                        {formatHistoryTimestamp(entry.timestamp)}
                      </td>
                      <td className="px-3 py-3">{entry.brandName}</td>
                      <td className="px-3 py-3">
                        <VerificationStatusBadge status={entry.overallStatus} />
                      </td>
                      <td className="px-3 py-3">
                        {entry.confidenceScore > 0 ? (
                          <ConfidenceScore value={entry.confidenceScore} />
                        ) : (
                          <span className="text-[var(--color-base)]">—</span>
                        )}
                      </td>
                      <td className="px-3 py-3 max-w-md text-[var(--color-base-dark)]">
                        {entry.reasoning}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </TreasuryLayout>
    </AuthGuard>
  );
}
