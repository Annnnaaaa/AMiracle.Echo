import { defineConfig } from "@playwright/test";

// Locally on Windows, use the installed Edge so no browser download is needed.
// CI installs Playwright's Chromium (`npx playwright install --with-deps chromium`)
// and leaves ECHO_E2E_CHANNEL unset.
const channel = process.env.ECHO_E2E_CHANNEL ?? (process.platform === "win32" && !process.env.CI ? "msedge" : undefined);

export default defineConfig({
  testDir: ".",
  testMatch: /.*\.spec\.mjs/,
  timeout: 30_000,
  reporter: process.env.CI ? "github" : "list",
  use: {
    channel,
    viewport: { width: 1000, height: 700 },
    deviceScaleFactor: 1,
    launchOptions: {
      args: [
        // Answer the "share this tab?" prompt automatically, as a user clicking Allow would.
        "--auto-accept-this-tab-capture",
        "--use-fake-ui-for-media-stream",
      ],
    },
  },
});
