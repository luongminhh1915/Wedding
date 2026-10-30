import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { getCanonicalUrl, getSeoMetadata } from "../src/utils/seoPolicy.mjs";

test("only explicitly public content and catalog routes are indexable", () => {
  for (const path of ["/", "/browse", "/vendors", "/listings/123", "/combo", "/combo/abc", "/cam-nang", "/wiki", "/wiki/ke-hoach-cuoi", "/invitation"]) {
    assert.equal(getSeoMetadata(path).indexable, true, `${path} should be public`);
  }

  for (const path of ["/login", "/register", "/reset-password", "/account", "/notifications", "/customer/dashboard", "/vendor/dashboard", "/admin/dashboard", "/planner", "/invitation/thiệp-rieng", "/private-slug", "/not-found"]) {
    assert.equal(getSeoMetadata(path).indexable, false, `${path} must not be indexed`);
    assert.equal(getCanonicalUrl(path), null, `${path} must not have a canonical URL`);
  }
});

test("aliases use one canonical URL and public detail URLs exclude query strings", () => {
  assert.equal(getCanonicalUrl("/vendors"), "https://weddingmatch.website/browse");
  assert.equal(getCanonicalUrl("/wiki"), "https://weddingmatch.website/cam-nang");
  assert.equal(getCanonicalUrl("/listings/123?from=search"), "https://weddingmatch.website/listings/123");
  assert.equal(getCanonicalUrl("/combo/abc/"), "https://weddingmatch.website/combo/abc");
});

test("sitemap contains only public routes and robots points Google to it", async () => {
  const html = await readFile(new URL("../index.html", import.meta.url), "utf8");
  const sitemap = await readFile(new URL("../public/sitemap.xml", import.meta.url), "utf8");
  const robots = await readFile(new URL("../public/robots.txt", import.meta.url), "utf8");
  assert.match(html, /<meta name="robots" content="index, follow, max-image-preview:large"\s*\/>/);
  assert.doesNotMatch(html, /noindex/i, "the crawlable initial HTML must not make Google skip JS rendering");
  assert.match(sitemap, /https:\/\/weddingmatch\.website\//);
  assert.doesNotMatch(sitemap, /customer|vendor\/dashboard|admin|notifications|\/invitation\//i);
  assert.match(robots, /User-agent:\s*\*/);
  assert.match(robots, /Allow:\s*\//);
  assert.match(robots, /Sitemap:\s*https:\/\/weddingmatch\.website\/sitemap\.xml/);
});
