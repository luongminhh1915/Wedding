import { test } from "node:test";
import assert from "node:assert/strict";
import { editableComboCart, comboEditIntent } from "../src/features/journey/combo/comboEditing.ts";
import {
  EMPTY_CART,
  startVendorCart,
} from "../src/features/journey/combo/cartModel.ts";
import {
  chooseDetailTemplate,
  comboDetailModel,
} from "../src/features/journey/combo/comboDetailModel.ts";

const item = {
  serviceItemId: "photo",
  vendorId: "shop",
  listingId: "listing",
  categoryId: "photo-category",
  selectionJson: '{"variantIds":["v"],"addOnIds":["a"],"hours":3}',
};
const order = {
  id: "submitted-order",
  status: "Submitted",
  items: [item],
  region: "Hồ Chí Minh",
  weddingDate: "2027-01-20T00:00:00",
  guestCount: 201,
  note: "Keep the original note",
};
const template = { id: "template", name: "Shop combo", ownerVendorId: "shop" };

test("dashboard edit actions route ordinary and staged drafts as drafts, never as self-revisions", () => {
  assert.deepEqual(comboEditIntent(order), { revision: order });
  const draft = { ...order, id: "draft", status: "Draft" };
  assert.deepEqual(comboEditIntent(draft), { draft });
  const staged = { ...draft, revisionOfId: order.id };
  assert.deepEqual(comboEditIntent(staged), { draft: staged });
});

test("editing a submitted combo restores configuration but never reuses its order ID as a draft", () => {
  const next = editableComboCart(order, template, order.id);
  assert.equal(next.draftId, undefined);
  assert.equal(next.revisionOfId, order.id);
  assert.equal(next.note, order.note);
  assert.equal(next.vendorId, "shop");
  assert.equal(next.weddingDate, "2027-01-20");
  assert.equal(next.guestCount, 201);
  assert.deepEqual(next.selections[0], {
    categoryId: "photo-category",
    serviceItemId: "photo",
    variantIds: ["v"],
    addOnIds: ["a"],
    hours: 3,
  });
  assert.equal(order.status, "Submitted");
});
test("reopening a saved revision draft keeps both its draft identity and original order link", () => {
  const next = editableComboCart(
    {
      ...order,
      id: "new-draft",
      status: "Draft",
      revisionReason: "Change photo package",
    },
    template,
    order.id,
  );
  assert.equal(next.draftId, "new-draft");
  assert.equal(next.revisionOfId, order.id);
  assert.equal(next.revisionReason, "Change photo package");
});
test("a mismatched template never carries another supplier's template or discount scope into editing", () => {
  const next = editableComboCart(
    order,
    { ...template, ownerVendorId: "other-shop" },
    order.id,
  );
  assert.equal(next.vendorId, "shop");
  assert.equal(next.templateId, null);
  assert.equal(next.templateVendorId, null);
});
test("starting a new provider cart clears stale revision and private note data", () => {
  const next = startVendorCart(
    {
      ...EMPTY_CART,
      revisionOfId: "old-order",
      revisionReason: "old-reason",
      note: "old-private-note",
      region: "HCM",
    },
    "other-shop",
  );
  assert.equal(next.revisionOfId, undefined);
  assert.equal(next.note, undefined);
  assert.equal(next.region, "HCM");
});
test("choosing a fresh preset never accidentally submits it as a revision of another order", () => {
  const data = {
    categories: [
      {
        categoryId: "photo-category",
        name: "Photo",
        options: [
          {
            ...item,
            title: "Photo",
            price: 100,
            unit: "Package",
            variants: [],
            addOns: [],
          },
        ],
      },
    ],
    templates: [
      {
        ...template,
        items: [
          {
            categoryId: "photo-category",
            serviceItemId: "photo",
            isOptional: false,
          },
        ],
      },
    ],
  };
  const next = chooseDetailTemplate(
    {
      ...EMPTY_CART,
      revisionOfId: "old-order",
      revisionReason: "old-reason",
      note: "old-note",
    },
    comboDetailModel(data, "template"),
  );
  assert.equal(next.revisionOfId, undefined);
  assert.equal(next.note, undefined);
});
