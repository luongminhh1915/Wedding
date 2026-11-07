import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";

if (!globalThis.localStorage) {
  const store = new Map();
  globalThis.localStorage = {
    getItem: (key) => store.get(key) ?? null,
    setItem: (key, val) => store.set(key, String(val)),
    removeItem: (key) => store.delete(key),
    clear: () => store.clear(),
  };
}

import { useWeddingCartStore } from "../src/store/weddingCartStore.ts";

describe("Unified Wedding Cart Store", () => {
  beforeEach(() => {
    useWeddingCartStore.getState().clearCart();
  });

  it("starts with an empty cart", () => {
    const store = useWeddingCartStore.getState();
    assert.equal(store.items.length, 0);
    assert.equal(store.getItemCount(), 0);
    assert.equal(store.getTotalAmount(), 0);
  });

  it("adds items and increments quantities on repeated adds", () => {
    const store = useWeddingCartStore.getState();

    store.addItem({
      id: "listing-1",
      listingId: "listing-1",
      title: "Gói Phóng Sự Cưới Lumiere",
      vendorBrandName: "Lumiere Studio",
      categoryName: "Quay phim & Chụp ảnh cưới",
      price: 12000000,
      priceFormatted: "12.000.000đ",
    });

    assert.equal(useWeddingCartStore.getState().items.length, 1);
    assert.equal(useWeddingCartStore.getState().getItemCount(), 1);
    assert.equal(useWeddingCartStore.getState().getTotalAmount(), 12000000);

    // Add same item again -> increments quantity
    useWeddingCartStore.getState().addItem({
      id: "listing-1",
      listingId: "listing-1",
      title: "Gói Phóng Sự Cưới Lumiere",
      vendorBrandName: "Lumiere Studio",
      categoryName: "Quay phim & Chụp ảnh cưới",
      price: 12000000,
      priceFormatted: "12.000.000đ",
    });

    assert.equal(useWeddingCartStore.getState().items.length, 1);
    assert.equal(useWeddingCartStore.getState().getItemCount(), 2);
    assert.equal(useWeddingCartStore.getState().getTotalAmount(), 24000000);
  });

  it("updates quantity and removes item when quantity reaches zero", () => {
    const store = useWeddingCartStore.getState();

    store.addItem({
      id: "listing-2",
      listingId: "listing-2",
      title: "Trang Trí Hoa Tươi The Little Flower",
      vendorBrandName: "The Little Flower",
      categoryName: "Trang trí tiệc cưới",
      price: 8000000,
      priceFormatted: "8.000.000đ",
    });

    store.updateQuantity("listing-2", 3);
    assert.equal(useWeddingCartStore.getState().getItemCount(), 3);
    assert.equal(useWeddingCartStore.getState().getTotalAmount(), 24000000);

    store.updateQuantity("listing-2", 0);
    assert.equal(useWeddingCartStore.getState().items.length, 0);
    assert.equal(useWeddingCartStore.getState().getItemCount(), 0);
  });

  it("removes specific items and clears cart", () => {
    const store = useWeddingCartStore.getState();

    store.addItem({
      id: "listing-a",
      listingId: "listing-a",
      title: "Dịch vụ A",
      vendorBrandName: "NCC A",
      categoryName: "Cat A",
      price: 5000000,
      priceFormatted: "5.000.000đ",
    });

    store.addItem({
      id: "listing-b",
      listingId: "listing-b",
      title: "Dịch vụ B",
      vendorBrandName: "NCC B",
      categoryName: "Cat B",
      price: 3000000,
      priceFormatted: "3.000.000đ",
    });

    assert.equal(useWeddingCartStore.getState().items.length, 2);

    store.removeItem("listing-a");
    assert.equal(useWeddingCartStore.getState().items.length, 1);
    assert.equal(useWeddingCartStore.getState().items[0].id, "listing-b");

    store.clearCart();
    assert.equal(useWeddingCartStore.getState().items.length, 0);
  });

  it("updates customer notes for individual items", () => {
    const store = useWeddingCartStore.getState();

    store.addItem({
      id: "listing-c",
      listingId: "listing-c",
      title: "Váy cưới công chúa",
      vendorBrandName: "Rose Bridal",
      categoryName: "Váy cưới & Vest",
      price: 7000000,
      priceFormatted: "7.000.000đ",
    });

    store.updateNotes("listing-c", "Yêu cầu size M, tông màu trắng ngà");
    assert.equal(
      useWeddingCartStore.getState().items[0].notes,
      "Yêu cầu size M, tông màu trắng ngà"
    );
  });
});
