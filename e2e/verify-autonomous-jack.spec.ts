import { expect, test } from "@playwright/test";

const demoEmail = "demo.agent@label-verify.demo";
const demoPassword = process.env.DEMO_AGENT_PASSWORD ?? "LocalTestPassword1!";

test("login, load samples, standard verify Jack ODP against application values", async ({ page }) => {
  await page.goto("/login/");
  await page.getByLabel("Email").fill(demoEmail);
  await page.getByLabel("Password").fill(demoPassword);
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.waitForURL(/\/(verify\/)?$/);

  await page.goto("/verify/");
  await page.getByRole("button", { name: "Load samples" }).click();
  await expect(page.getByText("05-odp-jack-daniels-old-no7.png")).toBeVisible({ timeout: 15_000 });

  const jackRow = page.locator("section.parameter-card").filter({
    hasText: "05-odp-jack-daniels-old-no7.png",
  });
  await expect(jackRow).toBeVisible();

  const removeButtons = page.getByRole("button", { name: "Remove" });
  while ((await removeButtons.count()) > 1) {
    const row = page.locator("section.parameter-card").filter({
      hasNotText: "05-odp-jack-daniels-old-no7.png",
    }).first();
    await row.getByRole("button", { name: "Remove" }).click();
  }

  await page.getByRole("button", { name: /Verify labels \(1\)/ }).click();
  await expect(page.getByText(/pass|review|fail/i).first()).toBeVisible({ timeout: 90_000 });
  await expect(page.getByText("05-odp-jack-daniels-old-no7.png")).toBeVisible();
});
