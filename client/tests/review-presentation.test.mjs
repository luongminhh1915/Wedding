import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ts from "typescript";
import { reviewEditComparisonComplete, reviewEditSnapshots, reviewPhotos, reviewTargetLabel, reviewTargetName, reviewTextEditPayload, reviewTransactionLabel } from "../src/features/customer/reviewPresentationModel.ts";

const review = () => ({ targetType: "Service", listingTitle: "Dịch vụ chụp ảnh", vendorBrandName: "Studio QA", comboTemplateName: "Combo QA", contractCode: "HD-QA", bookingContractId: "contract-qa", comboOrderId: null });
const edit = () => ({
  id: "edit-qa", reviewId: "review-qa", attemptNumber: 1,
  beforeContent: "Nội dung đang được công khai của khách hàng", beforeRating: 5,
  afterContent: "Nội dung đề nghị chỉnh sửa của khách hàng", afterRating: 3,
  beforePhotosJson: '["https://images.example.invalid/original.jpg"]', afterPhotosJson: "[]",
  beforeQualityRating: 5, afterQualityRating: 4,
  beforeServiceRating: 4, afterServiceRating: 3,
  beforePunctualityRating: 3, afterPunctualityRating: 2,
  beforeValueRating: 2, afterValueRating: 1,
  review: { ...review(), photosJson: '["https://images.example.invalid/current.jpg"]' },
});

test("typed target names ignore empty Listing titles and never label a Shop with an unrelated Service", () => {
  assert.equal(reviewTargetName({ ...review(), targetType: "Shop", listingTitle: "Sai đối tượng" }), "Studio QA");
  assert.equal(reviewTargetName({ ...review(), targetType: "Combo", listingTitle: "" }), "Combo QA");
  assert.equal(reviewTargetName({ ...review(), targetType: "Combo", comboTemplateName: " ", listingTitle: " Tên Combo API cũ " }), "Tên Combo API cũ");
  assert.equal(reviewTargetName({ ...review(), targetType: "Shop", vendorBrandName: " " }), "Shop chưa có tên");
  assert.equal(reviewTargetName({ ...review(), targetType: "Combo", comboTemplateName: null, listingTitle: " " }), "Combo chưa có tên");
  assert.equal(reviewTargetName({ ...review(), listingTitle: " " }), "Dịch vụ chưa có tên");
  assert.equal(reviewTargetName({ ...review(), targetType: undefined, listingTitle: " Dịch vụ cũ " }), "Dịch vụ cũ");
  assert.deepEqual([undefined, "Service", "Shop", "Combo"].map(reviewTargetLabel), ["Dịch vụ", "Dịch vụ", "Shop", "Combo"]);
});

test("transaction context prefers the stable ComboOrder and handles missing historical contract codes honestly", () => {
  assert.equal(reviewTransactionLabel({ ...review(), comboOrderId: "combo-order-qa" }), "Đơn combo combo-order-qa");
  assert.equal(reviewTransactionLabel(review()), "Hợp đồng HD-QA");
  assert.equal(reviewTransactionLabel({ ...review(), contractCode: " " }), "Hợp đồng contract-qa");
  assert.equal(reviewTransactionLabel({ ...review(), contractCode: null, bookingContractId: null }), "Đánh giá lịch sử");
});

test("text-only edit payload omits optional photos and scores instead of sending null/empty replacements", () => {
  const payload = reviewTextEditPayload(3, "  Nội dung đề nghị chỉnh sửa của khách hàng  ");
  assert.deepEqual(payload, { rating: 3, content: "Nội dung đề nghị chỉnh sửa của khách hàng" });
  assert.deepEqual(Object.keys(payload), ["rating", "content"]);
  for (const field of ["photosJson", "qualityRating", "serviceRating", "punctualityRating", "valueRating", "targetType", "customerId"])
    assert.equal(Object.hasOwn(payload, field), false);
});

test("comparison reads all persisted before/after fields without substituting current Review or deriving averages", () => {
  const data = edit(), original = structuredClone(data);
  const [before, after] = reviewEditSnapshots(data);
  assert.deepEqual(before.criteria.map(item => item.value), [5, 4, 3, 2]);
  assert.deepEqual(after.criteria.map(item => item.value), [4, 3, 2, 1]);
  assert.equal(before.rating, 5); assert.equal(after.rating, 3);
  assert.equal(before.photos.photos[0].reference, "https://images.example.invalid/original.jpg");
  assert.deepEqual(after.photos.photos, []);
  assert.deepEqual(data, original);
});

test("photo comparison distinguishes empty, missing, malformed and unsafe references without opening unsafe links", () => {
  for (const value of [null, "", "[]"]) assert.deepEqual(reviewPhotos(value), { state: "ready", photos: [] });
  assert.equal(reviewPhotos(undefined).state, "unavailable");
  for (const value of ["not-json", "{}", "[1]"]) assert.equal(reviewPhotos(value).state, "invalid");
  const safe = reviewPhotos('["/uploads/review.jpg","https://images.example.invalid/review.jpg"]');
  assert.deepEqual(safe.photos.map(photo => photo.href), ["/uploads/review.jpg", "https://images.example.invalid/review.jpg"]);
  for (const source of ["javascript:alert(1)", "//foreign.example.invalid/a.jpg", "/uploads/%2e%2e/private.jpg", "https://user:secret@example.invalid/photo.jpg"]) {
    const result = reviewPhotos(JSON.stringify([source]));
    assert.equal(result.photos[0].reference, source); assert.equal(result.photos[0].href, null);
  }
});

test("missing comparison fields fail closed, while explicit nullable scores are not mistaken for missing data", () => {
  assert.equal(reviewEditComparisonComplete(edit()), true);
  const blank = edit();
  for (const side of ["before", "after"]) {
    blank[`${side}PhotosJson`] = null;
    for (const criterion of ["QualityRating", "ServiceRating", "PunctualityRating", "ValueRating"]) blank[`${side}${criterion}`] = null;
  }
  assert.equal(reviewEditComparisonComplete(blank), true);
  for (const field of ["beforePhotosJson", "afterPhotosJson", "beforeQualityRating", "afterServiceRating", "beforePunctualityRating", "afterValueRating"]) {
    const missing = edit(); delete missing[field];
    assert.equal(reviewEditComparisonComplete(missing), false, field);
  }
  const malformed = edit(); malformed.afterPhotosJson = "{}";
  assert.equal(reviewEditComparisonComplete(malformed), false);
});

// Render the actual exported comparison component. Queue hooks and its unrelated ReviewList
// are inert dependencies here; these tests do not claim to exercise moderation/session actions.
const source = await readFile(new URL("../src/features/admin/ReviewModerationQueue.tsx", import.meta.url), "utf8");
let componentJs = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX } }).outputText;
for (const dependency of ["react", "react/jsx-runtime", "lucide-react"])
  componentJs = componentJs.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
for (const [dependency, file] of [["../customer/reviewTransport", "../src/features/customer/reviewTransport.ts"],
  ["../customer/reviewPresentationModel", "../src/features/customer/reviewPresentationModel.ts"], ["../journey/combo/comboMedia", "../src/features/journey/combo/comboMedia.ts"]])
  componentJs = componentJs.replace(`from "${dependency}"`, `from "${new URL(file, import.meta.url).href}"`);
const inert = `data:text/javascript,${encodeURIComponent('export const ReviewList = () => { throw new Error("Not part of comparison render"); }; export const useModerateReviewEdit = ReviewList; export const useModerationReviews = ReviewList; export const usePendingReviewEdits = ReviewList;')}`;
for (const dependency of ["../customer/components/reviews/ReviewList", "../customer/hooks/useReviews"])
  componentJs = componentJs.replace(`from "${dependency}"`, `from "${inert}"`);
const { ReviewEditComparison } = await import(`data:text/javascript;base64,${Buffer.from(componentJs).toString("base64")}`);
const render = data => renderToStaticMarkup(createElement(ReviewEditComparison, { edit: data }));

test("moderator comparison renders both complete snapshots, four criteria and explicit photo removal", () => {
  const html = render(edit());
  for (const text of ["ĐANG CÔNG KHAI", "BẢN SỬA", edit().beforeContent, edit().afterContent, "Ảnh (1)", "Ảnh (0)", "Không có ảnh."])
    assert.equal(html.includes(text), true, text);
  for (const label of ["Chất lượng", "Phục vụ", "Đúng hẹn", "Chi phí"])
    assert.equal(html.split(`<dt class="text-slate-500">${label}</dt>`).length - 1, 2, label);
  assert.equal(html.includes('href="https://images.example.invalid/original.jpg"'), true);
  assert.equal(html.includes("current.jpg"), false);
  assert.equal(html.includes('role="alert"'), false);
});

test("incomplete snapshots render an alert, and null scores differ from unavailable old API fields", () => {
  const data = edit(); delete data.afterQualityRating; data.beforeQualityRating = null;
  data.afterPhotosJson = '["javascript:alert(1)"]';
  const html = render(data);
  for (const text of ['role="alert"', "Chưa có dữ liệu", "Không chấm", "Liên kết ảnh không an toàn"])
    assert.equal(html.includes(text), true, text);
  assert.equal(html.includes('href="javascript:'), false);
});
