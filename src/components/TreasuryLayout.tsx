import Image from "next/image";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteNav } from "@/components/SiteNav";

type TreasuryLayoutProps = {
  children: React.ReactNode;
  title?: string;
  subtitle?: string;
  showNav?: boolean;
};

export const TreasuryLayout = ({
  children,
  title,
  subtitle,
  showNav = true,
}: TreasuryLayoutProps) => {
  return (
    <div className="flex min-h-screen flex-col bg-[var(--color-base-lightest)] text-[var(--color-base-darkest)]">
      <header className="border-b border-[var(--color-primary-dark)] bg-[var(--color-primary-darker)] text-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-2 px-4 py-4">
          <div className="flex shrink-0 items-center gap-2 sm:gap-4">
            <Image
              src="/treasury-seal.png"
              alt="U.S. Department of the Treasury seal"
              width={48}
              height={48}
              className="shrink-0 sm:h-14 sm:w-14"
              priority
            />
            <div className="shrink-0">
              <p className="whitespace-nowrap text-[10px] font-semibold uppercase tracking-[0.12em] text-[var(--color-accent-cool-lightest)] sm:text-xs sm:tracking-[0.14em]">
                U.S. Department of the Treasury
              </p>
              <p className="whitespace-nowrap text-base font-bold leading-tight sm:text-xl">
                Label Verification Portal
              </p>
            </div>
          </div>
          {showNav ? <SiteNav /> : null}
        </div>
      </header>

      <main className="flex-1">
        {title ? (
          <div className="mx-auto max-w-6xl px-4 pt-8">
            <h2 className="text-3xl font-bold text-[var(--color-primary-darker)]">{title}</h2>
            {subtitle ? (
              <p className="mt-2 max-w-3xl text-[var(--color-base-dark)]">{subtitle}</p>
            ) : null}
          </div>
        ) : null}
        {children}
      </main>

      <SiteFooter />
    </div>
  );
};
