"use client";

type ErrorPageProps = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function ErrorPage({ error, reset }: ErrorPageProps) {
  return (
    <div className="mx-auto max-w-lg px-4 py-16 text-center">
      <h2 className="text-2xl font-bold text-[var(--color-primary-darker)]">
        Something went wrong
      </h2>
      <p className="mt-3 text-sm text-[var(--color-base-dark)]">
        {error.message || "An unexpected error occurred while loading this page."}
      </p>
      <button
        type="button"
        onClick={reset}
        className="mt-6 rounded bg-[var(--color-primary-darker)] px-4 py-2 text-sm font-semibold text-white hover:bg-[var(--color-primary-dark)] focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-[var(--color-primary-dark)]"
      >
        Try again
      </button>
    </div>
  );
}
