import type { NextConfig } from "next";

const isProdBuild = process.env.NODE_ENV === "production";
const apiPort = process.env.LABEL_VERIFY_PLAN_API_PORT ?? "8082";

const nextConfig: NextConfig = {
  ...(isProdBuild
    ? {
        output: "export",
        distDir: "backend/LabelVerification.Api/wwwroot",
      }
    : {}),
  trailingSlash: true,
  skipTrailingSlashRedirect: true,
  images: {
    unoptimized: true,
  },
  async rewrites() {
    if (process.env.NODE_ENV === "development") {
      return [
        {
          source: "/api/:path*",
          destination: `http://localhost:${apiPort}/api/:path*`,
        },
        {
          source: "/health/:path*",
          destination: `http://localhost:${apiPort}/health/:path*`,
        },
        {
          source: "/samples/:path*",
          destination: `http://localhost:${apiPort}/samples/:path*`,
        },
      ];
    }

    return [];
  },
};

export default nextConfig;
