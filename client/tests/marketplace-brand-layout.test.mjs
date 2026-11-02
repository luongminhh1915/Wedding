import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const home = await readFile(new URL("../src/features/home/HomePage.tsx", import.meta.url), "utf8");
const browse = await readFile(new URL("../src/features/customer/BrowsePage.tsx", import.meta.url), "utf8");
const login = await readFile(new URL("../src/features/auth/components/LoginForm.tsx", import.meta.url), "utf8");
const register = await readFile(new URL("../src/features/auth/components/RegisterForm.tsx", import.meta.url), "utf8");
const dashboard = await readFile(new URL("../src/features/customer/CustomerDashboard.tsx", import.meta.url), "utf8");
const marketplaceCss = await readFile(new URL("../src/styles/marketplace-experience.css", import.meta.url), "utf8");

test("homepage discovery search is integrated into the marketplace hero", () => {
  const search = home.indexOf("className=\"marketplace-search-at-top home-marketplace-search\"");
  const hero = home.indexOf("className=\"wm-hero marketplace-hero\"");
  const heroTop = home.indexOf("className=\"marketplace-hero-top\"");
  const categories = home.indexOf("className=\"marketplace-categories section-width\"");

  assert.notEqual(search, -1);
  assert.ok(hero < heroTop && heroTop < search && search < categories);
  assert.match(home, /hero-romantic-couple\.png/);
});

test("service catalog filters stay above the browse introduction", () => {
  const search = browse.indexOf("className=\"marketplace-search-at-top browse-marketplace-search\"");
  const title = browse.indexOf("className=\"page-intro service-browse-intro\"");

  assert.notEqual(search, -1);
  assert.ok(title > search);
});

test("the supplied Bé Hý artwork is the shared mascot; previous assets remain unused fallbacks", async () => {
  const mascot = await readFile(new URL("../public/wedding-match/be-hy-mascot.png", import.meta.url));

  assert.equal(mascot.subarray(0, 8).toString("hex"), "89504e470d0a1a0a");
  for (const source of [login, register, dashboard]) {
    assert.match(source, /\/wedding-match\/be-hy-mascot\.png/);
    assert.match(source, /className="wm-mascot-image"/);
    assert.doesNotMatch(source, /\/wedding-match\/(?:be-hy\.svg|milo\.png)/);
  }
});

test("the second transparent Bé Hý pose appears beside the service marketplace introduction", async () => {
  const flowers = await readFile(new URL("../public/wedding-match/be-hy-flowers.png", import.meta.url));

  assert.equal(flowers.subarray(0, 8).toString("hex"), "89504e470d0a1a0a");
  assert.equal(flowers[25], 6, "the flower pose should retain its alpha channel");
  assert.match(browse, /className="service-browse-intro-top"/);
  assert.match(browse, /src="\/wedding-match\/be-hy-flowers\.png"/);
  assert.match(browse, /alt="Bé Hý vui vẻ cầm hoa chào bạn khám phá dịch vụ cưới"/);
});

test("homepage puts the real marketplace before labeled planning examples, without the old mascot demo", () => {
  assert.ok(home.indexOf('className="marketplace-featured section-width"') < home.indexOf('id="cong-cu"'));
  assert.match(home, /featuredListings\.data\.items\.map/);
  assert.match(home, /MINH HỌA CÔNG CỤ/);
  assert.match(home, /Ảnh danh mục minh họa phong cách dịch vụ/);
  assert.doesNotMatch(home, /marketplace-art-window|marketplace-art-mascot|be-hy-mascot|be-hy-flowers|<video/i);
});

test("browse filters sit in a top toolbar like combo discovery and results stay API-backed", async () => {
  const filterToolbar = await readFile(new URL("../src/features/customer/components/BrowseFilterSidebar.tsx", import.meta.url), "utf8");
  const filterPosition = browse.indexOf("<BrowseFilterSidebar");
  const resultsHeading = browse.indexOf("className=\"service-results-heading\"");
  assert.ok(filterPosition > browse.indexOf("<CategoryBar") && filterPosition < resultsHeading);
  assert.doesNotMatch(browse, /browse-results-layout/);
  assert.match(browse, /result\.items\.map/);
  assert.match(filterToolbar, /onChange\(next\)/);
  assert.match(filterToolbar, /is-expanded/);
  assert.match(filterToolbar, /service-price-presets/);
  assert.match(filterToolbar, /Sắp xếp/);
  assert.match(marketplaceCss, /\.browse-filter-panel\.is-expanded \.browse-filter-content\s*\{\s*display:\s*grid/);
});

test("service detail navigation only targets real gallery, overview, portfolio, review and contact sections", async () => {
  const detail = await readFile(new URL("../src/features/customer/ListingDetailPage.tsx", import.meta.url), "utf8");
  const serviceDetails = await readFile(new URL("../src/features/customer/components/ServiceDetails.tsx", import.meta.url), "utf8");
  for (const id of ["service-gallery", "service-overview", "service-reviews", "service-contact"]) {
    assert.match(detail, new RegExp(`id=\\"${id}\\"`));
    assert.match(detail, new RegExp(`href=\\"#${id}\\"`));
  }
  assert.match(serviceDetails, /id="service-portfolio"/);
});

test("editorial page searches and filters only fetched CMS articles", async () => {
  const articles = await readFile(new URL("../src/features/journey/ArticlesPage.tsx", import.meta.url), "utf8");
  assert.match(articles, /useArticles\(\)/);
  assert.match(articles, /data\.filter\(article/);
  assert.match(articles, /article\.thumbnailUrl/);
  assert.match(articles, /categories\.map/);
  assert.doesNotMatch(articles, /mock|placeholder article/i);
});
