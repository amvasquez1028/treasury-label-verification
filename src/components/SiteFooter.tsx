import Link from "next/link";

export const SiteFooter = () => {
  return (
    <footer className="site-footer mt-auto border-t border-[var(--color-primary-dark)] bg-[var(--color-primary-darker)] text-white">
      <div className="mx-auto flex max-w-6xl flex-col items-start justify-between gap-3 px-4 py-6 sm:flex-row sm:items-center">
        <p className="text-xs text-[var(--color-accent-cool-lightest)]/80">
          Label Verification Portal — Demo application for alcohol beverage label verification workflows.
        </p>
        <div className="flex flex-wrap items-center gap-4">
          <Link
            href="https://home.treasury.gov/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-sm font-semibold text-[var(--color-accent-cool-lightest)] underline-offset-2 hover:text-white hover:underline focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-white"
          >
            Treasury Homepage
          </Link>
        </div>
      </div>
    </footer>
  );
};
