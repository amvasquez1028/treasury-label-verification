"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { logout } from "@/lib/api";

const navItems = [
  { href: "/", label: "Home" },
  { href: "/verify/", label: "Verify" },
  { href: "/history/", label: "History" },
  { href: "/guidelines/", label: "Guidelines" },
  { href: "/security/", label: "Security" },
];

const isActivePath = (pathname: string, href: string): boolean => {
  if (href === "/") {
    return pathname === "/" || pathname === "";
  }

  return pathname.startsWith(href.replace(/\/$/, ""));
};

export const SiteNav = () => {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    const handlePointerDown = (event: MouseEvent | TouchEvent) => {
      const target = event.target as Node;
      if (menuRef.current && !menuRef.current.contains(target)) {
        setMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("touchstart", handlePointerDown);

    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("touchstart", handlePointerDown);
    };
  }, [menuOpen]);

  const handleSignOut = async () => {
    setMenuOpen(false);
    await logout();
    window.location.href = "/login/";
  };

  const handleToggleMenu = () => {
    setMenuOpen((current) => !current);
  };

  const handleNavClick = () => {
    setMenuOpen(false);
  };

  return (
    <div ref={menuRef} className="relative shrink-0 lg:w-auto">
      <div className="flex justify-end lg:hidden">
        <button
          type="button"
          className="inline-flex items-center justify-center rounded border border-white/40 p-2 text-white hover:bg-white/10 focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-white"
          aria-label={menuOpen ? "Close menu" : "Open menu"}
          aria-expanded={menuOpen}
          aria-controls="primary-navigation"
          onClick={handleToggleMenu}
        >
          <svg
            className="h-6 w-6"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
          >
            {menuOpen ? (
              <path strokeLinecap="round" d="M6 6l12 12M18 6L6 18" />
            ) : (
              <>
                <path strokeLinecap="round" d="M4 7h16" />
                <path strokeLinecap="round" d="M4 12h16" />
                <path strokeLinecap="round" d="M4 17h16" />
              </>
            )}
          </svg>
        </button>
      </div>

      <div
        id="primary-navigation"
        className={`${menuOpen ? "mt-3 flex" : "hidden"} absolute right-0 top-full z-20 min-w-[12rem] flex-col gap-2 rounded-md border border-white/20 bg-[var(--color-primary-darker)] p-3 shadow-lg lg:static lg:mt-0 lg:flex lg:flex-row lg:items-center lg:gap-4 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0 lg:shadow-none`}
      >
        <nav
          aria-label="Primary"
          className="flex flex-col gap-1 lg:flex-row lg:items-center"
        >
          {navItems.map((item) => {
            const active = isActivePath(pathname, item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={handleNavClick}
                aria-current={active ? "page" : undefined}
                className={`rounded px-3 py-2 text-sm font-semibold transition-colors focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-white ${
                  active
                    ? "bg-[var(--color-primary-dark)] text-white"
                    : "text-[var(--color-accent-cool-lightest)] hover:bg-[var(--color-primary-dark)] hover:text-white"
                }`}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <button
          type="button"
          onClick={handleSignOut}
          className="rounded border border-white/40 px-3 py-2 text-sm font-semibold text-white hover:bg-white/10 focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-white lg:ml-2"
          aria-label="Sign out"
        >
          Sign out
        </button>
      </div>
    </div>
  );
};
