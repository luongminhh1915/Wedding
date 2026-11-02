import test from "node:test";
import assert from "node:assert/strict";
import { comboCardActions, comboCardModel, comboMatchesFilter } from "../src/features/journey/customerOrders/comboOrderModel.ts";

const order = (status = "Submitted") => ({
  id: "order-1", code: "CB001", status, region: "Hồ Chí Minh", createdAt: "2026-10-17T00:00:00Z", total: 9000000,
  items: [{ id: "line-1", listingId: "listing-1", serviceItemId: "item-1", vendorId: "vendor-1", vendorName: "Studio thật", title: "Gói ảnh thật", price: 10000000, discountShare: 1000000, quantity: 1, unit: "Package", status: "Sent" }],
});
const metadata = (extra = {}) => ({
  kind: "combo", id: "order-1", isArchived: false, requests: [],
  capabilities: { canEdit: false, canWithdraw: false, canArchive: false, canRestore: false },
  media: { title: "Combo ngày vui", vendorName: "Studio thật", imageUrl: "/uploads/cover.jpg", galleryUrls: ["/uploads/cover.jpg", "/uploads/photo.jpg"], items: [{ listingId: "listing-1", serviceItemId: "item-1", title: "Ảnh", imageUrl: "/uploads/item.jpg", galleryUrls: ["/uploads/item.jpg"] }] },
  ...extra,
});

test("missing lifecycle metadata never fabricates edit, withdrawal or archive permissions", () => {
  assert.deepEqual(comboCardActions(undefined), []);
  assert.deepEqual(comboCardModel(order("Draft")).actions, []);
  assert.deepEqual(comboCardActions(metadata({ kind: "contract", capabilities: { canEdit: true, canWithdraw: true, canArchive: true, canRestore: true } })), []);
});
test("actions come only from explicit server capabilities", () => {
  assert.deepEqual(comboCardActions(metadata({ capabilities: { canEdit: true, canWithdraw: true, canArchive: false, canRestore: false } })), ["edit", "withdraw"]);
  assert.deepEqual(comboCardActions(metadata({ capabilities: { canEdit: false, canWithdraw: false, canArchive: true, canRestore: false } })), ["archive"]);
});
test("archived cards expose restoration only and are hidden from ordinary lists", () => {
  const archived = metadata({ isArchived: true, capabilities: { canEdit: true, canWithdraw: true, canArchive: true, canRestore: true } });
  assert.deepEqual(comboCardActions(archived), ["restore"]);
  assert.equal(comboMatchesFilter(order(), archived, "all"), false);
  assert.equal(comboMatchesFilter(order(), archived, "active"), false);
  assert.equal(comboMatchesFilter(order(), archived, "archived"), true);
  assert.equal(comboCardModel(order(), archived).displayStatus, "Archived");
});
test("draft, live and terminal requests go to separate filters without dropping history", () => {
  assert.equal(comboMatchesFilter(order("Draft"), metadata(), "draft"), true);
  assert.equal(comboMatchesFilter(order("Draft"), metadata(), "active"), false);
  for (const status of ["Submitted", "InProgress", "Quoted", "PartiallyContracted"]) {
    assert.equal(comboMatchesFilter(order(status), metadata(), "active"), true);
    assert.equal(comboMatchesFilter(order(status), metadata(), "all"), true);
  }
  for (const status of ["Cancelled", "Completed", "Expired", "Superseded"]) {
    assert.equal(comboMatchesFilter(order(status), metadata(), "closed"), true);
    assert.equal(comboMatchesFilter(order(status), metadata(), "active"), false);
    assert.equal(comboMatchesFilter(order(status), metadata(), "all"), true);
  }
});
test("a superseded snapshot is historical even if the old status still says Submitted", () => {
  const old = metadata({ supersededById: "new-order" });
  assert.equal(comboMatchesFilter(order(), old, "closed"), true);
  assert.equal(comboMatchesFilter(order(), old, "active"), false);
  assert.equal(comboCardModel(order(), old).displayStatus, "Superseded");
});
test("cards use actual provider/service photos, deduplicate and keep the saved price", () => {
  const value = order(); const result = comboCardModel(value, metadata());
  assert.equal(result.vendorName, "Studio thật"); assert.equal(result.title, "Combo ngày vui");
  assert.deepEqual(result.photos, ["/uploads/cover.jpg", "/uploads/photo.jpg", "/uploads/item.jpg"]);
  assert.deepEqual(result.items[0].photos, ["/uploads/item.jpg"]);
  assert.equal(result.items[0].price - result.items[0].discountShare, 9000000);
  assert.equal(value.items.length, 1); assert.equal(value.items[0].title, "Gói ảnh thật");
});
test("service-image selection prefers the exact service ID over another service sharing its listing", () => {
  const meta = metadata(); meta.media.items.unshift({ listingId: "listing-1", serviceItemId: "other-item", title: "Other", imageUrl: "/other.jpg", galleryUrls: [] });
  assert.deepEqual(comboCardModel(order(), meta).items[0].photos, ["/uploads/item.jpg"]);
});
test("orders without media show no invented image or provider and preserve all services", () => {
  const result = comboCardModel(order()); assert.deepEqual(result.photos, []); assert.deepEqual(result.items[0].photos, []);
  assert.equal(result.vendorName, "Studio thật"); assert.equal(result.title, "Combo của Studio thật"); assert.equal(result.items.length, 1);
});
