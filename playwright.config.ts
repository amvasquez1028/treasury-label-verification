import { defineConfig } from "@playwright/test";

const apiPort = process.env.LABEL_VERIFY_PLAN_API_PORT ?? "8082";
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://127.0.0.1:${apiPort}`;

export default defineConfig({
  testDir: "e2e",
  timeout: 120_000,
  expect: { timeout: 90_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  use: {
    baseURL,
    trace: "on-first-retry",
  },
  webServer: process.env.PLAYWRIGHT_BASE_URL
    ? undefined
    : {
        command: `dotnet run --project backend/LabelVerification.Api/LabelVerification.Api.csproj --configuration Release --no-build`,
        url: `${baseURL}/health/live`,
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
        env: {
          ASPNETCORE_URLS: `http://0.0.0.0:${apiPort}`,
          ASPNETCORE_ENVIRONMENT: "Development",
          SEED_DEMO_USERS: "true",
          DEMO_AGENT_PASSWORD: process.env.DEMO_AGENT_PASSWORD ?? "LocalTestPassword1!",
          DEMO_PARALLEL_PASSWORD: process.env.DEMO_PARALLEL_PASSWORD ?? "LocalTestPassword1!",
          Ocr__FlatArtworkEnginePoolSize: "6",
          Ocr__UseFieldBandTargetedOcr: "true",
          Ocr__FlatArtworkMaxOcrSide: "1200",
          Ocr__PerLabelWallClockMs: "8000",
          Ocr__SubmissionGradeTargetMs: "3500",
          Ocr__TimeoutSeconds: "8",
        },
      },
});
