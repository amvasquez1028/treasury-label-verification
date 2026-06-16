"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { TreasuryLayout } from "@/components/TreasuryLayout";
import { login } from "@/lib/api";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      await login(email, password);
      router.push("/");
    } catch (err) {
      const message = err instanceof Error ? err.message : "";
      if (message.includes("fetch failed") || message.includes("ECONNREFUSED") || message.includes("Failed to proxy")) {
        setError("Cannot reach the API. Start the backend on port 8082, then try again.");
      } else if (message) {
        setError(message);
      } else {
        setError("Invalid credentials or account not approved.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <TreasuryLayout title="Sign in" showNav={false}>
      <div className="mx-auto max-w-6xl px-4 pb-10">
        <form
          onSubmit={handleSubmit}
          className="parameter-card max-w-md"
          aria-label="Sign in form"
        >
          <label className="mb-4 block text-sm font-semibold" htmlFor="email">
            Email
            <input
              id="email"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="form-field mt-1 w-full rounded border border-[var(--color-base-lighter)] px-3 py-2"
            />
          </label>

          <label className="mb-4 block text-sm font-semibold" htmlFor="password">
            Password
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="form-field mt-1 w-full rounded border border-[var(--color-base-lighter)] px-3 py-2"
            />
          </label>

          {error ? (
            <p className="mb-4 rounded border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
              {error}
            </p>
          ) : null}

          <button
            type="submit"
            disabled={loading}
            className="w-full rounded bg-[var(--color-primary-darker)] px-4 py-2 font-semibold text-white hover:bg-[var(--color-primary-dark)] disabled:opacity-60"
          >
            {loading ? "Signing in..." : "Sign in"}
          </button>
        </form>
      </div>
    </TreasuryLayout>
  );
}
