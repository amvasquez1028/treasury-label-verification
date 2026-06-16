import type { Metadata } from "next";
import localFont from "next/font/local";
import "./globals.css";

const publicSans = localFont({
  src: [
    { path: "../../public/fonts/PublicSans-Regular.woff2", weight: "400", style: "normal" },
    { path: "../../public/fonts/PublicSans-Bold.woff2", weight: "700", style: "normal" },
  ],
  variable: "--font-public-sans",
  fallback: ["Segoe UI", "system-ui", "sans-serif"],
});

export const metadata: Metadata = {
  title: "Treasury Label Verification",
  description: "Verify alcohol beverage label artwork against TTB requirements",
  icons: {
    icon: "/treasury-seal.png",
    shortcut: "/treasury-seal.png",
    apple: "/treasury-seal.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={`${publicSans.variable} antialiased`}>{children}</body>
    </html>
  );
}
