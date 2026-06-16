"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { TreasuryLayout } from "@/components/TreasuryLayout";

export default function HomePage() {
  return (
    <AuthGuard>
      <TreasuryLayout>
        <div className="mx-auto max-w-6xl px-4 py-10">
          <section className="parameter-card">
            <h2 className="text-2xl font-bold text-[var(--color-primary-darker)]">
              Alcohol Beverage Label Verification
            </h2>
            <p className="mt-4 text-[var(--color-base-dark)]">
              The Label Verification Portal helps validate artwork against expected Certificate of
              Label Approval (COLA) standards. Upload label images and parameters to receive an
              automated OCR confidence score for meeting required elements.
            </p>
            <p className="mt-4 text-[var(--color-base-dark)]">
              This demo application applies field-level matching with confidence scoring. Results are
              saved to your browser history for audit review.
            </p>
            <p className="mt-4 text-sm text-[var(--color-base-dark)]">
              Please see the{" "}
              <Link
                href="/guidelines/"
                className="font-semibold text-[var(--color-primary-darker)] underline"
              >
                Guidelines
              </Link>{" "}
              page for more details on frameworks and a tutorial.
            </p>
            <div className="mt-8">
              <Link
                href="/verify/"
                className="inline-block rounded bg-[var(--color-primary-darker)] px-6 py-3 font-semibold text-white hover:bg-[var(--color-primary-dark)] focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-[var(--color-primary-darker)]"
              >
                Start Verification
              </Link>
            </div>
          </section>
        </div>
      </TreasuryLayout>
    </AuthGuard>
  );
}
