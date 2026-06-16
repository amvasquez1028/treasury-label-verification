"use client";

type GlobalErrorProps = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function GlobalError({ error, reset }: GlobalErrorProps) {
  return (
    <html lang="en">
      <body className="bg-[var(--color-base-lightest)] text-[var(--color-base-darkest)] antialiased">
        <div className="mx-auto max-w-lg px-4 py-16 text-center">
          <h2 className="text-2xl font-bold text-[var(--color-primary-darker)]">
            Application error
          </h2>
          <p className="mt-3 text-sm text-[var(--color-base-dark)]">
            {error.message || "A critical error occurred."}
          </p>
          <button
            type="button"
            onClick={reset}
            className="mt-6 rounded bg-[var(--color-primary-darker)] px-4 py-2 text-sm font-semibold text-white hover:bg-[var(--color-primary-dark)]"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
