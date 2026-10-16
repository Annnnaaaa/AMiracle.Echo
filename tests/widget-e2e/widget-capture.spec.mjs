// Real-browser tests for the widget's two screenshot choices:
//   "Add screenshot" — captures this tab through getDisplayMedia (the browser's own
//                      share prompt is auto-accepted by a launch flag);
//   "Attach image"   — the file picker, for a screenshot the user already has.
// The page and the Echo API are both served by page.route, so no server is needed.
import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const ORIGIN = "http://echo-e2e.localhost";
const WIDGET = readFileSync(fileURLToPath(new URL("../../src/AMiracle.Echo.Server/Resources/widget.js", import.meta.url)), "utf8");
const FEEDBACK_ID = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

// A solid red page, so a captured frame is easy to check pixel by pixel.
const PAGE = `<!doctype html><html lang="en"><head><meta charset="utf-8">
<style>html,body{margin:0;height:100%;background:#ff0000}</style></head>
<body><script src="${ORIGIN}/echo/widget.js" data-project-id="p" data-public-key="ekp_test" data-bubble="off"></script></body></html>`;

// 1x1 PNG for the file-picker path.
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");

/** Serves the page + widget and records what the widget sends to the API. */
async function mountWidget(page, { screenshotStatuses = [204] } = {}) {
  const sent = { feedback: null, uploads: [] };
  await page.route(`${ORIGIN}/**`, async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    if (path === "/") return route.fulfill({ contentType: "text/html", body: PAGE });
    if (path === "/echo/widget.js") return route.fulfill({ contentType: "application/javascript", body: WIDGET });
    if (path === "/api/v1/feedbacks" && req.method() === "POST") {
      sent.feedback = req.postDataJSON();
      return route.fulfill({
        status: 201, contentType: "application/json",
        body: JSON.stringify({ id: FEEDBACK_ID, uploadScreenshotUrl: `/api/v1/feedbacks/${FEEDBACK_ID}/screenshot` }),
      });
    }
    if (path === `/api/v1/feedbacks/${FEEDBACK_ID}/screenshot`) {
      sent.uploads.push({ contentType: req.headers()["content-type"], body: req.postDataBuffer() });
      const status = screenshotStatuses[Math.min(sent.uploads.length - 1, screenshotStatuses.length - 1)];
      return route.fulfill({ status, body: "" });
    }
    return route.fulfill({ status: 404, body: "" });
  });
  await page.goto(`${ORIGIN}/`);
  await page.waitForFunction(() => !!window.AMiracleEcho);
  await page.evaluate(() => window.AMiracleEcho.open());
  await expect(page.locator("amiracle-echo .panel.open")).toBeVisible();
  return sent;
}

async function send(page, text) {
  await page.locator("amiracle-echo textarea").fill(text);
  await page.locator("amiracle-echo .js-send").click();
}

test("Add screenshot captures this tab, without the panel in the shot", async ({ page }) => {
  const sent = await mountWidget(page);
  const panelBox = await page.locator("amiracle-echo .panel").boundingBox();

  // Spy on the real getDisplayMedia so we can see what was asked for.
  await page.evaluate(() => {
    const real = navigator.mediaDevices.getDisplayMedia.bind(navigator.mediaDevices);
    window.__displayCalls = [];
    navigator.mediaDevices.getDisplayMedia = (opts) => { window.__displayCalls.push(opts); return real(opts); };
  });

  await page.locator("amiracle-echo .js-shot").click();
  await expect(page.locator("amiracle-echo .js-shot-preview img.thumb")).toBeVisible({ timeout: 15_000 });
  const calls = await page.evaluate(() => window.__displayCalls);
  expect(calls).toHaveLength(1);
  expect(calls[0].preferCurrentTab).toBe(true);
  expect(calls[0].audio).toBe(false);

  await send(page, "Captured from the tab");
  await expect(page.locator("amiracle-echo .panel")).toContainText("Thanks!");

  expect(sent.feedback.willUploadScreenshot).toBe(true);
  expect(sent.uploads).toHaveLength(1);
  const shot = sent.uploads[0];
  expect(shot.contentType).toBe("image/jpeg");
  expect(shot.body.subarray(0, 2).toString("hex")).toBe("ffd8"); // JPEG magic

  // Decode the uploaded frame in the page and look where the panel was: it must be
  // the red page, not the panel — the widget hides itself while capturing.
  const probe = await page.evaluate(async ({ b64, box }) => {
    const img = new Image();
    img.src = "data:image/jpeg;base64," + b64;
    await img.decode();
    const c = document.createElement("canvas");
    c.width = img.naturalWidth; c.height = img.naturalHeight;
    const ctx = c.getContext("2d");
    ctx.drawImage(img, 0, 0);
    const sx = img.naturalWidth / window.innerWidth;
    const at = (x, y) => Array.from(ctx.getImageData(Math.round(x * sx), Math.round(y * sx), 1, 1).data);
    return {
      width: img.naturalWidth,
      panelCentre: at(box.x + box.width / 2, box.y + box.height / 2),
      corner: at(10, 10),
    };
  }, { b64: shot.body.toString("base64"), box: panelBox });

  expect(probe.width).toBeGreaterThan(100);
  const isRed = ([r, g, b]) => r > 200 && g < 60 && b < 60;
  expect(isRed(probe.corner), `corner pixel ${probe.corner}`).toBe(true);
  expect(isRed(probe.panelCentre), `panel-area pixel ${probe.panelCentre}`).toBe(true);
});

test("Attach image opens the file picker and never asks to share the screen", async ({ page }) => {
  const sent = await mountWidget(page);
  await page.evaluate(() => {
    window.__displayCalls = 0;
    navigator.mediaDevices.getDisplayMedia = () => { window.__displayCalls++; return Promise.reject(new Error("should not be called")); };
  });

  const chooser = page.waitForEvent("filechooser");
  await page.locator("amiracle-echo .js-attach").click();
  await (await chooser).setFiles({ name: "earlier-screenshot.png", mimeType: "image/png", buffer: PNG });
  await expect(page.locator("amiracle-echo .js-shot-preview img.thumb")).toBeVisible();

  await send(page, "Here's the screenshot I took earlier");
  await expect(page.locator("amiracle-echo .panel")).toContainText("Thanks!");
  expect(await page.evaluate(() => window.__displayCalls)).toBe(0);
  expect(sent.uploads).toHaveLength(1);
  expect(sent.uploads[0].contentType).toBe("image/png");
  expect(sent.uploads[0].body.equals(PNG)).toBe(true);
});

test("Without screen capture support only Attach image is offered", async ({ page }) => {
  await page.addInitScript(() => { delete Object.getPrototypeOf(navigator.mediaDevices).getDisplayMedia; });
  await mountWidget(page);
  await expect(page.locator("amiracle-echo .js-shot")).toHaveCount(0);
  await expect(page.locator("amiracle-echo .js-attach")).toBeVisible();
});

test("Dismissing the share prompt is not an error and restores the panel", async ({ page }) => {
  await mountWidget(page);
  await page.evaluate(() => {
    navigator.mediaDevices.getDisplayMedia = () => Promise.reject(new DOMException("dismissed", "NotAllowedError"));
  });
  await page.locator("amiracle-echo .js-shot").click();
  await expect(page.locator("amiracle-echo .panel")).toBeVisible();
  await expect(page.locator("amiracle-echo .js-error")).toBeHidden();
  await expect(page.locator("amiracle-echo .js-shot-preview")).toBeHidden();
});

test("A capture that never sends a frame gives up with a message instead of hiding the panel forever", async ({ page }) => {
  await mountWidget(page);
  await page.evaluate(() => {
    // A canvas stream at 0 fps only emits on requestFrame(), which nobody calls.
    navigator.mediaDevices.getDisplayMedia = async () => document.createElement("canvas").captureStream(0);
  });
  await page.locator("amiracle-echo .js-shot").click();
  await expect(page.locator("amiracle-echo .js-error")).toHaveText("Couldn't capture the screen.", { timeout: 10_000 });
  await expect(page.locator("amiracle-echo .panel")).toBeVisible();
  await expect(page.locator("amiracle-echo .js-shot-preview")).toBeHidden();
});

test("A host page can hide the bubble on phones with ::part, and still open the panel", async ({ page }) => {
  const hostCss = "@media (max-width: 767px) { amiracle-echo::part(bubble) { display: none; } }";
  await page.route(`${ORIGIN}/**`, async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === "/echo/widget.js") return route.fulfill({ contentType: "application/javascript", body: WIDGET });
    return route.fulfill({
      contentType: "text/html",
      body: `<!doctype html><html lang="en"><head><style>${hostCss}</style></head><body>
        <script src="${ORIGIN}/echo/widget.js" data-project-id="p" data-public-key="ekp_test"></script></body></html>`,
    });
  });

  await page.goto(`${ORIGIN}/`);
  const bubble = page.locator("amiracle-echo .bubble");
  await expect(bubble).toBeVisible(); // desktop width (1000px)

  await page.setViewportSize({ width: 390, height: 800 });
  await expect(bubble).toBeHidden();
  await page.evaluate(() => window.AMiracleEcho.open());
  await expect(page.locator("amiracle-echo .panel.open")).toBeVisible();

  await page.setViewportSize({ width: 1000, height: 700 });
  await expect(bubble).toBeVisible();
});

test("A screenshot upload that hits a 5xx is retried once", async ({ page }) => {
  const sent = await mountWidget(page, { screenshotStatuses: [503, 204] });
  const chooser = page.waitForEvent("filechooser");
  await page.locator("amiracle-echo .js-attach").click();
  await (await chooser).setFiles({ name: "s.png", mimeType: "image/png", buffer: PNG });
  await send(page, "retry me");
  await expect(page.locator("amiracle-echo .panel")).toContainText("Thanks!");
  expect(sent.uploads).toHaveLength(2);
  await expect(page.locator("amiracle-echo .panel")).not.toContainText("couldn't be uploaded");
});

test("A rejected screenshot says the message went through but the image didn't", async ({ page }) => {
  const sent = await mountWidget(page, { screenshotStatuses: [413] });
  const chooser = page.waitForEvent("filechooser");
  await page.locator("amiracle-echo .js-attach").click();
  await (await chooser).setFiles({ name: "big.png", mimeType: "image/png", buffer: PNG });
  await send(page, "too big");
  await expect(page.locator("amiracle-echo .panel")).toContainText("Your message was sent, but the screenshot couldn't be uploaded.");
  expect(sent.uploads).toHaveLength(1); // 4xx is final — no retry
});
