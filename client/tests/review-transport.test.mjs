import { test } from "node:test";
import assert from "node:assert/strict";
import axios, { AxiosError, isCancel } from "axios";
import { canRequestReviews, createReviewTransport, reviewErrorMessage, reviewQueryKey, reviewReadState } from "../src/features/customer/reviewTransport.ts";

const customer = { userId: "customer-a", role: "Customer", token: "synthetic-customer-token", revision: 1 };
const moderator = { userId: "moderator-a", role: "Moderator", token: "synthetic-moderator-token", revision: 2 };
const vendor = { userId: "vendor-a", role: "VendorOwner", token: "synthetic-vendor-token", revision: 3 };
const anonymous = { userId: null, role: null, token: null, revision: 4 };
const response = (config, data, status = 200) => ({ config, data, status, statusText: status === 200 ? "OK" : "Unavailable", headers: {} });
function fixture(session, adapter, baseURL = "https://api.example.invalid/wedding/") {
  let current = session;
  const client = axios.create({ baseURL, adapter });
  return { client, transport: createReviewTransport(client, () => current), change: next => { current = next; } };
}

test("public review reads retain configured cross-origin API base or same-origin proxy and forward abort signals", async () => {
  for (const baseURL of ["https://api.example.invalid/wedding/", ""]) {
    const requests = [];
    const f = fixture(anonymous, async config => { requests.push(config); return response(config, []); }, baseURL);
    const controller = new AbortController();
    assert.deepEqual(await f.transport.listing(anonymous, "listing-id", controller.signal), []);
    assert.deepEqual(await f.transport.vendor(anonymous, "vendor-id", controller.signal), []);
    assert.equal(f.client.getUri(requests[0]), `${baseURL}/api/reviews/listing/listing-id`.replace(/(?<!:)\/{2,}/g, "/"));
    assert.equal(requests[0].signal, controller.signal);
    assert.equal(requests[0].headers.get("Authorization"), null);
    assert.match(requests[1].url, /\/api\/reviews\/vendor\/vendor-id$/);
  }
});

test("public listing/vendor and staff moderation HTTP failures reject with real server errors rather than empty success", async () => {
  for (const [session, method, args] of [[anonymous, "listing", ["listing"]], [anonymous, "vendor", ["vendor"]], [moderator, "moderation", []]]) {
    const f = fixture(session, async config => {
      throw new AxiosError("Request failed", AxiosError.ERR_BAD_RESPONSE, config, null, response(config, { message: "Server đang bảo trì đánh giá" }, 503));
    });
    await assert.rejects(f.transport[method](session, ...args), /Server đang bảo trì đánh giá/);
  }
});

test("malformed success payloads fail visibly while actual empty review arrays remain valid", async () => {
  for (const data of ["<html>proxy error</html>", {}, null]) {
    const f = fixture(anonymous, async config => response(config, data));
    await assert.rejects(f.transport.listing(anonymous, "listing"), /Không thể tải đánh giá dịch vụ/);
  }
  const f = fixture(anonymous, async config => response(config, []));
  assert.deepEqual(await f.transport.listing(anonymous, "listing"), []);
});

test("private reads and mutations require their correct authenticated actor without issuing unauthorized requests", async () => {
  let calls = 0;
  for (const session of [anonymous, customer, vendor, moderator]) {
    const f = fixture(session, async config => { calls++; return response(config, []); });
    for (const [method, access, args] of [["eligible", "customer", []], ["moderation", "moderator", []], ["submit", "customer", [{}]],
      ["approve", "moderator", ["review"]], ["reject", "moderator", ["review", "Reason"]], ["reply", "vendor", ["review", "Reply"]]]) {
      if (!canRequestReviews(access, session)) await assert.rejects(f.transport[method](session, ...args), /không có quyền/);
    }
  }
  assert.equal(calls, 0);
  assert.equal(canRequestReviews("moderator", { ...moderator, role: "SuperAdmin" }), true);
});

test("private requests pin the initiating authStore session token and preserve existing routes and payloads", async () => {
  const requests = [];
  const adapter = async config => { requests.push(config); return response(config, config.method === "get" ? [] : { message: "Saved", review: { id: "review" } }); };
  const f = fixture(customer, adapter);
  await f.transport.eligible(customer);
  const payload = { listingId: "listing", bookingContractId: "completed-contract", rating: 5, content: "Original review content", qualityRating: 5 };
  await f.transport.submit(customer, payload);
  f.change(moderator); await f.transport.moderation(moderator); await f.transport.approve(moderator, "review"); await f.transport.reject(moderator, "review", "Server reason");
  f.change(vendor); await f.transport.reply(vendor, "review", "Vendor response");
  assert.equal(requests[0].headers.get("Authorization"), `Bearer ${customer.token}`);
  assert.equal(requests[2].headers.get("Authorization"), `Bearer ${moderator.token}`);
  assert.equal(requests[5].headers.get("Authorization"), `Bearer ${vendor.token}`);
  assert.deepEqual(JSON.parse(requests[1].data), payload);
  assert.deepEqual(JSON.parse(requests[4].data), { reason: "Server reason" });
  assert.deepEqual(JSON.parse(requests[5].data), { reply: "Vendor response" });
  assert.deepEqual(requests.map(config => config.url), ["/api/reviews/eligible-contracts", "/api/reviews", "/api/reviews/moderation/pending", "/api/reviews/review/approve", "/api/reviews/review/reject", "/api/reviews/review/reply"]);
});

test("query keys separate identity, role and session revision without exposing credentials or breaking invalidation prefixes", () => {
  for (const kind of ["listing-reviews", "vendor-reviews", "eligible-contracts", "moderation-reviews"]) {
    const first = reviewQueryKey(kind, customer, "target");
    assert.deepEqual(first.slice(0, 2), [kind, "target"]);
    for (const changed of [{ ...customer, userId: "customer-b" }, { ...customer, role: "Moderator" }, { ...customer, revision: 9 }, anonymous])
      assert.notDeepEqual(first, reviewQueryKey(kind, changed, "target"));
    assert.equal(JSON.stringify(first).includes(customer.token), false);
  }
});

test("a changed or revoked session rejects a stale read/write before any request is sent", async () => {
  let calls = 0;
  const f = fixture(customer, async config => { calls++; return response(config, []); });
  f.change({ ...customer, token: "replacement-token", revision: 2 });
  await assert.rejects(f.transport.eligible(customer), isCancel);
  await assert.rejects(f.transport.submit(customer, { listingId: "listing" }), isCancel);
  assert.equal(calls, 0);
});

test("responses completing after an account switch cannot populate another session or report mutation success", async () => {
  for (const method of ["eligible", "submit"]) {
    let finish;
    let started;
    const ready = new Promise(resolve => { started = resolve; });
    const f = fixture(customer, config => new Promise(resolve => { finish = () => resolve(response(config, method === "eligible" ? [{ contractId: "private-old-contract" }] : { id: "old-review" })); started(); }));
    const request = method === "eligible" ? f.transport.eligible(customer) : f.transport.submit(customer, { listingId: "listing" });
    await ready; f.change({ ...customer, userId: "customer-b", token: "next-identity", revision: 2 }); finish();
    await assert.rejects(request, isCancel);
  }
});

test("aborted review reads remain cancellations and do not become empty results or generic errors", async () => {
  let finish;
  let started;
  const ready = new Promise(resolve => { started = resolve; });
  const f = fixture(customer, config => new Promise(resolve => { finish = () => resolve(response(config, [])); started(); }));
  const controller = new AbortController();
  const request = f.transport.eligible(customer, controller.signal);
  await ready; controller.abort(); finish();
  await assert.rejects(request, isCancel);
});

test("loading and failed reads never present the successful empty state, including a failed refresh with cached rows", () => {
  assert.equal(reviewReadState(true, false, 0), "loading");
  assert.equal(reviewReadState(false, true, 0), "error");
  assert.equal(reviewReadState(false, true, 3), "error");
  assert.equal(reviewReadState(false, false, 0), "empty");
  assert.equal(reviewReadState(false, false, 3), "ready");
  const failure = new AxiosError("Network", AxiosError.ERR_NETWORK);
  assert.equal(reviewErrorMessage(failure, "Retry service"), "Retry service");
  assert.equal(reviewErrorMessage(new Error("Domain validation")), "Domain validation");
});

test("independent targets and edit moderation use the correct actor, routes and unchanged optional payloads", async () => {
  const requests = [];
  const f = fixture(customer, async config => {
    requests.push(config);
    return response(config, config.method === 'get' && !config.url.includes('/target/') ? [] : { id: 'review' });
  });
  await f.transport.eligibleTargets(customer);
  await f.transport.mine(customer);
  await f.transport.submitShop(customer, { comboOrderId: 'custom-order', rating: 4, content: 'Shop completed transaction' });
  await f.transport.submitCombo(customer, { comboOrderId: 'versioned-order', rating: 5, content: 'Exact purchased package' });
  const textOnly = { rating: 2, content: 'Edit text without removing photos or detailed scores' };
  await f.transport.submitEdit(customer, 'review/id', textOnly);
  await f.transport.submitEdit(customer, 'review/id', { ...textOnly, photosJson: '[]', qualityRating: 3 });
  await f.transport.target(customer, 'Combo', 'target/id', 2, 10, 5, true, 'rating-high');
  f.change(moderator);
  await f.transport.pendingEdits(moderator);
  await f.transport.approveEdit(moderator, 'edit/id');
  await f.transport.rejectEdit(moderator, 'edit/id', 'Please clarify');
  assert.deepEqual(requests.map(item => item.url), [
    '/api/reviews/eligible-targets', '/api/reviews/mine', '/api/reviews/shop', '/api/reviews/combo',
    '/api/reviews/review%2Fid/edit-requests', '/api/reviews/review%2Fid/edit-requests',
    '/api/reviews/target/Combo/target%2Fid?page=2&pageSize=10&sort=rating-high&rating=5&verifiedOnly=true',
    '/api/reviews/moderation/pending-edits', '/api/reviews/moderation/edit-requests/edit%2Fid/approve',
    '/api/reviews/moderation/edit-requests/edit%2Fid/reject',
  ]);
  assert.deepEqual(JSON.parse(requests[4].data), textOnly);
  assert.equal(Object.hasOwn(JSON.parse(requests[4].data), 'photosJson'), false);
  assert.deepEqual(JSON.parse(requests[5].data), { ...textOnly, photosJson: '[]', qualityRating: 3 });
  assert.deepEqual(JSON.parse(requests[9].data), { reason: 'Please clarify' });
  for (let index = 0; index < requests.length; index++)
    assert.equal(requests[index].headers.get('Authorization'), `Bearer ${index < 7 ? customer.token : moderator.token}`);
});

test("independent private reads and mutations deny foreign roles and stale edit results", async () => {
  let calls = 0;
  for (const session of [anonymous, vendor, customer, moderator]) {
    const f = fixture(session, async config => { calls++; return response(config, []); });
    for (const [method, access, args] of [['eligibleTargets', 'customer', []], ['mine', 'customer', []],
      ['submitShop', 'customer', [{}]], ['submitCombo', 'customer', [{}]], ['submitEdit', 'customer', ['review', {}]],
      ['pendingEdits', 'moderator', []], ['approveEdit', 'moderator', ['edit']], ['rejectEdit', 'moderator', ['edit', 'reason']]])
      if (!canRequestReviews(access, session)) await assert.rejects(f.transport[method](session, ...args), /không có quyền/);
  }
  assert.equal(calls, 0);
  let finish;
  let started;
  const ready = new Promise(resolve => { started = resolve; });
  const f = fixture(customer, config => new Promise(resolve => {
    finish = () => resolve(response(config, { id: 'old-session-review' })); started();
  }));
  const request = f.transport.submitEdit(customer, 'review', { rating: 4, content: 'Pending edit' });
  await ready;
  f.change(anonymous);
  finish();
  await assert.rejects(request, isCancel);
});
