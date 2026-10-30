import { test } from "node:test";
import assert from "node:assert/strict";
import {
  comboDetailModel,
  chooseDetailTemplate,
  detailBasePrice,
  previewDetailCatalog,
} from "../src/features/journey/combo/comboDetailModel.ts";
import { EMPTY_CART } from "../src/features/journey/combo/cartModel.ts";

const option = (serviceItemId, extra = {}) => ({
  serviceItemId,
  listingId: `listing-${serviceItemId}`,
  vendorId: "shop",
  vendorName: "Shop",
  title: serviceItemId,
  price: 1000000,
  unit: "Package",
  variants: [],
  addOns: [],
  imageUrl: `https://shop.example/${serviceItemId}.jpg`,
  ...extra,
});
const catalog = () => ({
  categories: [
    {
      categoryId: "venue",
      name: "Tiệc cưới",
      options: [option("venue-1", { unit: "Table" })],
    },
    {
      categoryId: "photo",
      name: "Ảnh cưới",
      options: [option("photo-1", { price: 3000000 })],
    },
  ],
  templates: [
    {
      id: "combo",
      name: "Combo shop",
      ownerVendorId: "shop",
      imageUrl: "https://shop.example/cover.jpg",
      items: [
        { categoryId: "venue", serviceItemId: "venue-1", isOptional: false },
        { categoryId: "photo", serviceItemId: "photo-1", isOptional: true },
      ],
    },
  ],
  vendors: [
    {
      id: "shop",
      name: "Shop",
      city: "Hà Nội",
      isVerified: true,
      ratingAvg: null,
      totalReviews: 0,
    },
  ],
});

test("detail groups included and optional services and derives category from its catalog container", () => {
  const model = comboDetailModel(catalog(), "combo");
  assert.equal(model.canChoose, true);
  assert.equal(model.required.length, 1);
  assert.equal(model.optional.length, 1);
  assert.equal(model.required[0].item.categoryId, "venue");
  assert.equal(detailBasePrice(model, 201), 21000000);
});
test("deep-link missing, cross-provider and unavailable combos cannot be chosen", () => {
  assert.equal(comboDetailModel(catalog(), "missing"), null);
  const mixed = catalog();
  mixed.categories[1].options[0].vendorId = "different-shop";
  const model = comboDetailModel(mixed, "combo");
  assert.equal(model.canChoose, false);
  assert.throws(() => chooseDetailTemplate(EMPTY_CART, model));
  const missing = catalog();
  missing.categories[0].options = [];
  assert.equal(comboDetailModel(missing, "combo").canChoose, false);
});
test("choosing a detail adds only included services and clears stale draft and upgrade references", () => {
  const old = {
    ...EMPTY_CART,
    draftId: "old-draft",
    selections: [{ serviceItemId: "old" }],
  };
  const next = chooseDetailTemplate(old, comboDetailModel(catalog(), "combo"), {
    city: "Hà Nội",
    weddingDate: "2027-03-20T18:00:00Z",
    guestCount: 201,
  });
  assert.deepEqual(
    next.selections.map((line) => line.serviceItemId),
    ["venue-1"],
  );
  assert.deepEqual(next.selections[0].variantIds, []);
  assert.equal(next.vendorId, "shop");
  assert.equal(next.templateId, "combo");
  assert.equal(next.draftId, undefined);
  assert.equal(next.weddingDate, "2027-03-20");
  assert.equal(old.selections[0].serviceItemId, "old");
});
test("choosing another package preserves existing event inputs rather than overwriting them", () => {
  const cart = {
    ...EMPTY_CART,
    region: "Hồ Chí Minh",
    weddingDate: "2027-02-01",
    guestCount: 111,
  };
  const next = chooseDetailTemplate(
    cart,
    comboDetailModel(catalog(), "combo"),
    { city: "Hà Nội", weddingDate: "2028-01-01", guestCount: 500 },
  );
  assert.equal(next.region, cart.region);
  assert.equal(next.weddingDate, cart.weddingDate);
  assert.equal(next.guestCount, 111);
});
test("detail gallery uses supplied catalog pictures only and deduplicates shared sources", () => {
  const data = catalog();
  data.vendors[0].coverImageUrl = data.templates[0].imageUrl;
  const model = comboDetailModel(data, "combo");
  assert.deepEqual(model.photos, [
    "https://shop.example/cover.jpg",
    "https://shop.example/venue-1.jpg",
    "https://shop.example/photo-1.jpg",
  ]);
  assert.equal(model.vendor.ratingAvg, null);
});

test("private detail uses only authorized revision options, preserves published cache and never permits cart selection", () => {
  const published = catalog(), before = structuredClone(published);
  const proposal = {
    ...published.templates[0], name: "Draft revised combo", moderationStatus: "Draft",
    content: { galleryUrls: ["https://shop.example/proposal-gallery.jpg"], terms: "Proposal terms" },
    previewOptions: [option("venue-1", { title: "Actual private component", price: 1700000, unit: "Table", description: "Authorized unpublished component" }), option("photo-1")],
  };
  const privateData = previewDetailCatalog(published, proposal);
  const model = comboDetailModel(privateData, "combo");
  assert.equal(model.template.name, "Draft revised combo");
  assert.equal(model.required[0].item.title, "Actual private component");
  assert.equal(model.required[0].item.description, "Authorized unpublished component");
  assert.equal(model.required[0].item.price, 1700000);
  assert.equal(model.readOnly, true);
  assert.throws(() => chooseDetailTemplate(EMPTY_CART, model));
  assert.ok(model.photos.includes("https://shop.example/proposal-gallery.jpg"));
  assert.equal(model.template.content.terms, "Proposal terms");
  assert.deepEqual(published, before);
  assert.equal(comboDetailModel(published, "combo").template.name, "Combo shop");
});

test("missing private options cannot fall back to publicly cached services with the same IDs", () => {
  const published = catalog();
  for (const previewOptions of [null, undefined, []]) {
    const privateData = previewDetailCatalog(published, { ...published.templates[0], previewOptions });
    const model = comboDetailModel(privateData, "combo");
    assert.equal(model.required[0].item, undefined);
    assert.equal(model.canChoose, false);
    assert.throws(() => chooseDetailTemplate(EMPTY_CART, model));
  }
});

test("private preview works without public builder and does not fabricate removed component records", () => {
  const proposal = { ...catalog().templates[0], previewOptions: [option("venue-1", { description: "Actual retained record" })] };
  const model = comboDetailModel(previewDetailCatalog(undefined, proposal), "combo");
  assert.equal(model.required[0].categoryName, "Hạng mục dịch vụ");
  assert.equal(model.required[0].item.description, "Actual retained record");
  assert.equal(model.optional[0].item, undefined);
  assert.equal(model.vendor, undefined);
  assert.equal(model.readOnly, true);
  assert.equal(model.canChoose, false);
});

test("private preview ignores options unrelated to revision slots and never replaces category vendor ownership checks", () => {
  const published = catalog();
  const proposal = { ...published.templates[0], previewOptions: [option("venue-1", { vendorId: "foreign" }), option("photo-1"), option("unrelated")] };
  const data = previewDetailCatalog(published, proposal);
  assert.equal(data.categories.flatMap(category => category.options).some(item => item.serviceItemId === "unrelated"), false);
  const model = comboDetailModel(data, "combo");
  assert.equal(model.required[0].valid, false);
  assert.equal(model.canChoose, false);
  assert.throws(() => chooseDetailTemplate(EMPTY_CART, model));
});
