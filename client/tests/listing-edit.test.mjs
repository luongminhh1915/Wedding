import { test } from "node:test";
import assert from "node:assert/strict";
import { hydrateListingEditor, isListingImageReference, listingImagePreview, listingUpdatePayload, moveListingCover, uploadedListingImages } from "../src/features/vendor-portal/components/listingEditModel.ts";

const local = "/uploads/listings/0123456789abcdef0123456789abcdef.png";
const detail = () => ({
  id: "listing-a", categoryId: "photo", title: "Ảnh cưới", minPrice: 12000000, maxPrice: 16000000,
  description: "Mô tả đầy đủ đã được nhà cung cấp lưu, không được thay bằng chuỗi rỗng.", location: "Hồ Chí Minh",
  media: [
    { id: "third", mediaUrl: "https://cdn.example.com/three.jpg", displayOrder: 2, isFeatured: false },
    { id: "first", mediaUrl: local, displayOrder: 0, isFeatured: true },
    { id: "second", mediaUrl: "http://legacy.example.com/two.jpg", displayOrder: 1, isFeatured: false },
  ],
});

test("listing edit hydrates full description and every stored photo in cover/display order without mutating cache", () => {
  const source = detail(); const unchanged = structuredClone(source);
  const editor = hydrateListingEditor(source, source.id, "https://api.example.com/");
  assert.equal(editor.form.description, source.description);
  assert.equal(editor.form.categoryId, "photo");
  assert.deepEqual(editor.images.map(image => image.id), ["first", "second", "third"]);
  assert.equal(editor.images[0].url, local);
  assert.equal(editor.images[0].preview, `https://api.example.com${local}`);
  assert.equal(editor.images[1].preview, "http://legacy.example.com/two.jpg");
  assert.deepEqual(source, unchanged);
});

test("incomplete or wrong listing details never produce an editable empty fallback", () => {
  assert.throws(() => hydrateListingEditor(null, "listing-a"));
  assert.throws(() => hydrateListingEditor(detail(), "listing-other"));
  assert.throws(() => hydrateListingEditor({ ...detail(), description: undefined }, "listing-a"));
  assert.throws(() => hydrateListingEditor({ ...detail(), media: undefined }, "listing-a"));
});

test("price-only changes retain full description and omit media instead of replacing the album", () => {
  const editor = hydrateListingEditor(detail(), "listing-a");
  const changed = { ...editor.form, minPrice: "12500000" };
  const payload = listingUpdatePayload(changed, editor.images, editor.images);
  assert.equal(payload.minPrice, 12500000);
  assert.equal(payload.description, detail().description);
  assert.equal("imageUrls" in payload, false);
  const old = [{ id: "legacy", url: "/old-existing-reference.jpg", preview: "" }];
  assert.equal("imageUrls" in listingUpdatePayload(changed, old, old), false);
});

test("selecting a new cover persists full reordered album while retaining all other service fields", () => {
  const editor = hydrateListingEditor(detail(), "listing-a");
  const reordered = moveListingCover(editor.images, 2);
  assert.deepEqual(reordered.map(image => image.id), ["third", "first", "second"]);
  assert.deepEqual(editor.images.map(image => image.id), ["first", "second", "third"]);
  assert.deepEqual(listingUpdatePayload(editor.form, reordered, editor.images).imageUrls, reordered.map(image => image.url));
  assert.equal(listingUpdatePayload(editor.form, reordered, editor.images).description, detail().description);
  assert.equal(moveListingCover(editor.images, -1), editor.images);
  assert.equal(moveListingCover(editor.images, 20), editor.images);
});

test("replacement payload rejects empty duplicate temporary executable traversal and oversized image references", () => {
  const editor = hydrateListingEditor(detail(), "listing-a");
  const bad = ["javascript:alert(1)", "data:image/png;base64,abc", "blob:https://site/id", "//evil.example/image.png", "/uploads/listings/../../web.config", "/uploads/listings/%252e%252e/web.config", "https://cdn.example.com/a/%2e%2e/secret.png", "https://name:pass@cdn.example.com/image.jpg", "https://cdn.example.com/a\n.png", `https://cdn.example.com/${"a".repeat(500)}.jpg`];
  for (const url of bad) {
    assert.equal(isListingImageReference(url), false, url);
    assert.throws(() => listingUpdatePayload(editor.form, [{ id: "new", url, preview: "" }], editor.images), undefined, url);
  }
  assert.throws(() => listingUpdatePayload(editor.form, [], editor.images));
  assert.throws(() => listingUpdatePayload(editor.form, [editor.images[0], editor.images[0]], editor.images));
  assert.throws(() => listingUpdatePayload(editor.form, Array.from({ length: 11 }, (_, index) => ({ id: `${index}`, url: `https://cdn.example.com/${index}.jpg`, preview: "" })), editor.images));
  assert.equal(isListingImageReference("https://cdn.example.com/image.jpg?w=1200"), true);
  assert.equal(isListingImageReference("http://legacy.example.com/image.jpg"), true);
  assert.equal(listingImagePreview("javascript:alert(1)", "https://api.example.com"), "");
});

test("uploads require complete real server URLs and never persist temporary local blob previews", () => {
  const items = [{ id: "new", preview: "blob:local-preview", url: "" }];
  assert.throws(() => uploadedListingImages(items, []));
  assert.throws(() => uploadedListingImages(items, ["blob:local-preview"]));
  const images = uploadedListingImages(items, [local]);
  assert.equal(images[0].url, local);
  assert.equal(images[0].preview, "blob:local-preview");
  assert.equal(items[0].url, "");
});
