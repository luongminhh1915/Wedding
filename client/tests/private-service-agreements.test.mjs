import { test } from "node:test";
import assert from "node:assert/strict";
import { privateAgreementActions, privateAgreementChatLead, privateAgreementDraft, privateAgreementEffective, privateAgreementPayload, privateAgreementResponse } from "../src/features/private-agreements/privateAgreementModel.ts";

const version = "A".repeat(64);
const line = () => ({ name: "  Chụp ảnh  ", quantity: 1, details: "  Hai nhiếp ảnh gia  " });
const workspace = () => ({ kind: "contract", targetId: "target", currentVersion: version, currentTotal: 8500000, originalContent: [line()], effectiveContent: [line()], history: [], canPropose: true, canRespond: false });
const proposal = () => ({ ...privateAgreementDraft(workspace()), note: "  Theo trao đổi trong chat  ", terms: "  Cùng tổng tiền đã chốt  " });

test("private proposal body allowlists content and server version without amount, ownership or commission fields", () => {
  const draft = { ...proposal(), contractValue: 1, commissionRate: .01, currentTotal: 1, customerId: "other", items: [{ ...line(), price: 1, serviceItemId: "other-shop" }] };
  const result = privateAgreementPayload(draft);
  assert.deepEqual(result, { expectedVersion: version, items: [{ name: "Chụp ảnh", quantity: 1, details: "Hai nhiếp ảnh gia" }], note: "Theo trao đổi trong chat", terms: "Cùng tổng tiền đã chốt" });
  assert.equal(draft.items[0].name, "  Chụp ảnh  ");
});

test("draft copies effective agreed composition and pins version without mutating original history", () => {
  const data = workspace(); data.effectiveContent = [{ name: "Nội dung riêng đã đồng ý", quantity: 2, details: "Đã xác nhận" }];
  const before = structuredClone(data), draft = privateAgreementDraft(data);
  draft.items[0].name = "Bản soạn mới";
  assert.deepEqual(data, before); assert.equal(draft.expectedVersion, data.currentVersion);
  assert.equal(draft.note, ""); assert.equal(draft.terms, "");
});

test("unread or malformed target versions cannot submit content or accept a stale version silently", () => {
  for (const expectedVersion of ["", "1", "A".repeat(63), "G".repeat(64)]) {
    assert.throws(() => privateAgreementPayload({ ...proposal(), expectedVersion }), /phiên bản/);
    assert.throws(() => privateAgreementResponse(expectedVersion, true, ""), /phiên bản/);
  }
  assert.deepEqual(privateAgreementResponse(version, true, "  Đồng ý  "), { expectedVersion: version, accept: true, note: "Đồng ý" });
  assert.deepEqual(privateAgreementResponse(version, false, ""), { expectedVersion: version, accept: false, note: null });
});

test("private composition limits match backend and do not accept empty or fractional quantities", () => {
  for (const items of [[], Array(31).fill(line()), [{ ...line(), name: " " }], [{ ...line(), name: "x".repeat(201) }], [{ ...line(), quantity: 0 }], [{ ...line(), quantity: 1.5 }], [{ ...line(), quantity: 10001 }], [{ ...line(), details: "x".repeat(10001) }]]) assert.throws(() => privateAgreementPayload({ ...proposal(), items }));
  for (const change of [{ note: " " }, { note: "x".repeat(2001) }, { terms: "x".repeat(4001) }]) assert.throws(() => privateAgreementPayload({ ...proposal(), ...change }));
  assert.throws(() => privateAgreementResponse(version, false, "x".repeat(2001)));
});

test("frozen service detail supports 10,000 characters while aggregate serialized composition is bounded", () => {
  const value = proposal(); value.items = [{ ...line(), details: "x".repeat(10000) }];
  assert.equal(privateAgreementPayload(value).items[0].details.length, 10000);
  assert.throws(() => privateAgreementPayload({ ...value, items: Array.from({ length: 4 }, () => ({ ...value.items[0] })) }), /40.000/);
});

test("source combo reference never replaces a contract's effective content or enters its proposal payload", () => {
  const data = workspace(); data.sourceComboId = "source-combo"; data.sourceComboContent = [{ name: "Accepted source combo reference", quantity: 2, details: "Reference only" }];
  const draft = privateAgreementDraft(data); draft.note = "Contract-specific proposal";
  assert.equal(draft.items[0].name, data.effectiveContent[0].name);
  const payload = privateAgreementPayload({ ...draft, sourceComboId: data.sourceComboId, sourceComboContent: data.sourceComboContent });
  assert.equal(Object.hasOwn(payload, "sourceComboId"), false); assert.equal(Object.hasOwn(payload, "sourceComboContent"), false);
  assert.notEqual(payload.items[0].name, data.sourceComboContent[0].name);
});

test("client actions require role and actual server capability and keep staff out of private review", () => {
  const data = workspace(); data.history = [{ id: "pending", sequence: 1, status: "PendingCustomer", items: [line()] }]; data.canRespond = true;
  assert.equal(privateAgreementActions(data, "Customer").respond, true);
  assert.equal(privateAgreementActions(data, "Customer").propose, false);
  assert.equal(privateAgreementActions(data, "VendorOwner").respond, false);
  assert.equal(privateAgreementActions(data, "VendorOwner").withdraw, true);
  for (const role of ["Moderator", "SuperAdmin", "Finance", "CustomerCare", undefined]) {
    const result = privateAgreementActions(data, role); assert.equal(result.propose, false); assert.equal(result.respond, false); assert.equal(result.withdraw, false);
  }
  data.blockedReason = "Đã hoàn tất";
  for (const role of ["Customer", "VendorOwner"]) {
    const result = privateAgreementActions(data, role); assert.equal(result.propose, false); assert.equal(result.respond, false); assert.equal(result.withdraw, false);
  }
  data.canWithdraw = true;
  assert.equal(privateAgreementActions(data, "VendorOwner").withdraw, true, "server may allow safe withdrawal of a stale proposal while content changes remain blocked");
  assert.equal(privateAgreementActions(data, "Customer").withdraw, false);
});

test("effective content points to newest accepted history, never a pending or rejected composition", () => {
  const data = workspace(); data.history = [{ id: "old", sequence: 1, status: "Accepted" }, { id: "new", sequence: 2, status: "Accepted" }, { id: "pending", sequence: 3, status: "PendingCustomer" }, { id: "rejected", sequence: 4, status: "Rejected" }];
  assert.equal(privateAgreementEffective(data).id, "new"); assert.equal(data.history[0].id, "old");
  assert.equal(privateAgreementEffective(workspace()), undefined);
});

test("combo chat requires a valid contextual lead or explicit customer selection", () => {
  const data = workspace();
  data.leadOptions = [{ leadId: "lead-a", listingTitle: "Photo" }, { leadId: "lead-b", listingTitle: "Venue" }];
  assert.equal(privateAgreementChatLead(data), null, "an aggregated combo must not select an arbitrary conversation");
  assert.equal(privateAgreementChatLead(data, "lead-b"), "lead-b", "a vendor lead card pins its own conversation");
  assert.equal(privateAgreementChatLead(data, "not-in-order"), null, "context must belong to this combo");
  assert.equal(privateAgreementChatLead(data, undefined, "lead-a"), "lead-a", "the customer can select an existing conversation");
  assert.equal(privateAgreementChatLead(data, undefined, "not-in-order"), null);
  data.leadId = "lead-b";
  assert.equal(privateAgreementChatLead(data), "lead-b");
});
