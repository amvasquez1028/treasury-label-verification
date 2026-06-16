import { Suspense } from "react";
import ApprovePageClient from "./ApprovePageClient";

export default function ApprovePage() {
  return (
    <Suspense
      fallback={
        <div className="mx-auto max-w-md px-4 py-10 text-sm text-[var(--color-base)]">
          Loading approval form…
        </div>
      }
    >
      <ApprovePageClient />
    </Suspense>
  );
}
