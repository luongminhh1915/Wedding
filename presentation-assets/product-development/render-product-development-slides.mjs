import fs from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const here = path.dirname(fileURLToPath(import.meta.url));
const htmlPath = path.join(here, "product-development-slides.html");
const renderTargets = [
  ["slide12", "slide12-development-journey.png"],
  ["slide13", "slide13-product-evolution.png"],
  ["slide14", "slide14-functional-workflows.png"],
  ["slide15", "slide15-public-deployment.png"],
];

await fs.access(htmlPath);
const browserCandidates = [
  process.env.CHROME_EXECUTABLE,
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
].filter(Boolean);
const executablePath = browserCandidates.find(existsSync);
const browser = await chromium.launch({
  headless: true,
  ...(executablePath ? { executablePath } : {}),
});

try {
  const page = await browser.newPage({
    viewport: { width: 1920, height: 1080 },
    deviceScaleFactor: 1,
  });
  await page.goto(pathToFileURL(htmlPath).href, { waitUntil: "networkidle" });
  await page.evaluate(() => document.fonts.ready);

  for (const [slideId, fileName] of renderTargets) {
    const slide = page.locator(`#${slideId}`);
    await slide.scrollIntoViewIfNeeded();
    await slide.screenshot({ path: path.join(here, fileName) });
    const box = await slide.boundingBox();
    if (!box || Math.round(box.width) !== 1920 || Math.round(box.height) !== 1080) {
      throw new Error(`${slideId} did not render at 1920×1080.`);
    }
  }
} finally {
  await browser.close();
}

console.log(`Rendered ${renderTargets.length} slide PNGs in ${here}`);
