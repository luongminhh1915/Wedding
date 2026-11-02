import { test } from "node:test";
import assert from "node:assert/strict";
import { EMPTY_REGISTRATION, normalizeRegistration, registrationApiError, validateRegistration, vendorRegistrationRequest } from "../src/features/auth/components/registrationValidation.ts";

const valid = { ...EMPTY_REGISTRATION, fullName: "Nguyễn Văn A", email: "customer@example.com", phoneNumber: "0912345678", password: "A long passphrase", confirmPassword: "A long passphrase", brandName: "Wedding QA", city: "Hồ Chí Minh", partnershipPolicyAccepted: true };

test("registration trims identity fields without altering passwords", () => {
  const result = normalizeRegistration({ ...valid, fullName: " Nguyễn Văn A ", email: " Customer@Example.COM ", phoneNumber: " 0912345678 ", brandName: " Wedding QA ", city: " Hà Nội ", password: " pass phrase ", confirmPassword: " pass phrase " });
  assert.equal(result.fullName, "Nguyễn Văn A");
  assert.equal(result.email, "customer@example.com");
  assert.equal(result.phoneNumber, "0912345678");
  assert.equal(result.brandName, "Wedding QA");
  assert.equal(result.city, "Hà Nội");
  assert.equal(result.password, " pass phrase ");
  assert.equal(result.confirmPassword, " pass phrase ");
});

test("customer and vendor registration validate only their required fields", () => {
  assert.deepEqual(validateRegistration(valid, "customer"), {});
  assert.deepEqual(validateRegistration(valid, "vendor"), {});
  const missingVendor = { ...valid, brandName: " ", city: " " };
  assert.deepEqual(validateRegistration(missingVendor, "customer"), {});
  assert.deepEqual(Object.keys(validateRegistration(missingVendor, "vendor")), ["brandName", "city"]);
});

test("registration rejects blank or overlong identities and malformed email or phone", () => {
  const errors = validateRegistration({ ...valid, fullName: " ", email: "a@@example.com", phoneNumber: "+84912345678", brandName: "x".repeat(201), city: "x".repeat(101) }, "vendor");
  assert.deepEqual(Object.keys(errors), ["fullName", "email", "phoneNumber", "brandName", "city"]);
  assert.ok(validateRegistration({ ...valid, fullName: "x".repeat(151) }, "customer").fullName);
  assert.ok(validateRegistration({ ...valid, email: `${"x".repeat(140)}@example.com` }, "customer").email);
  assert.ok(validateRegistration({ ...valid, phoneNumber: "1912345678" }, "customer").phoneNumber);
  assert.ok(validateRegistration({ ...valid, phoneNumber: "091 234 5678" }, "customer").phoneNumber);
});

test("new password minimum, UTF8 BCrypt limit and matching confirmation are enforced", () => {
  assert.ok(validateRegistration({ ...valid, password: "1234567", confirmPassword: "1234567" }, "customer").password);
  assert.equal(validateRegistration({ ...valid, password: "12345678", confirmPassword: "12345678" }, "customer").password, undefined);
  const atLimit = "ắ".repeat(24);
  assert.equal(new TextEncoder().encode(atLimit).length, 72);
  assert.equal(validateRegistration({ ...valid, password: atLimit, confirmPassword: atLimit }, "customer").password, undefined);
  assert.ok(validateRegistration({ ...valid, password: `${atLimit}a`, confirmPassword: `${atLimit}a` }, "customer").password);
  assert.ok(validateRegistration({ ...valid, confirmPassword: "A different passphrase" }, "customer").confirmPassword);
  assert.ok(validateRegistration({ ...valid, confirmPassword: "" }, "customer").confirmPassword);
  for (const password of ["        ", "\t\t\t\t\t\t\t\t", "\u2003".repeat(8)])
    assert.ok(validateRegistration({ ...valid, password, confirmPassword: password }, "customer").password);
});

test("vendor registration succeeds without selecting groups or loading a group catalog", () => {
  assert.deepEqual(validateRegistration(valid, "vendor"), {});
  assert.equal("businessGroupIds" in EMPTY_REGISTRATION, false);
  for (const businessGroupIds of [[], ["unknown-group"], ["duplicate", "duplicate"]]) {
    const input = { ...valid, businessGroupIds };
    assert.deepEqual(validateRegistration(input, "vendor"), {});
    assert.equal("businessGroupIds" in vendorRegistrationRequest(input), false);
  }
  assert.equal(EMPTY_REGISTRATION.partnershipPolicyAccepted, false);
});

test("vendor policy acknowledgment is required without changing customer signup fields", () => {
  const unconfirmed = { ...valid, partnershipPolicyAccepted: false };
  assert.ok(validateRegistration(unconfirmed, "vendor").partnershipPolicyAccepted);
  assert.deepEqual(validateRegistration(unconfirmed, "customer"), {});
  assert.throws(() => vendorRegistrationRequest(unconfirmed), /xác nhận/);
});

test("vendor payload is allowlisted and never sends editable financial rates or policy version", () => {
  const input = { ...valid, email: " CUSTOMER@Example.com ", city: "TP. HCM", businessGroupIds: ["old-group"], commissionRate: .01, policyVersion: "forged", role: "SuperAdmin" };
  const request = vendorRegistrationRequest(input);
  assert.deepEqual(Object.keys(request).sort(), ["fullName", "email", "phoneNumber", "password", "brandName", "city", "partnershipPolicyAccepted"].sort());
  assert.equal(request.email, "customer@example.com");
  assert.equal(request.city, "Hồ Chí Minh");
  assert.equal("businessGroupIds" in request, false);
  assert.equal(request.partnershipPolicyAccepted, true);
  assert.equal("commissionRate" in request, false);
  assert.equal("role" in request, false);
});

test("vendor registration requires a current province while customer onboarding remains separate", () => {
  assert.equal(EMPTY_REGISTRATION.city, "", "new partners must choose rather than silently default to another city");
  for (const city of ["Huế", "Bắc Ninh", "Đồng Nai", "Hồ Chí Minh", "Tỉnh Đắk Lắk", "TP. HCM"])
    assert.equal(validateRegistration({ ...valid, city }, "vendor").city, undefined, city);
  for (const city of ["", "Ho Chi Minh arbitrary", "Bình Dương", "Nước ngoài"])
    assert.ok(validateRegistration({ ...valid, city }, "vendor").city, city);
  assert.equal(normalizeRegistration({ ...valid, city: " TP. HCM " }).city, "Hồ Chí Minh");
  assert.equal(validateRegistration({ ...valid, city: "" }, "customer").city, undefined);
});

test("API validation dictionaries and arrays are flattened and deduplicated", () => {
  assert.equal(registrationApiError({ response: { data: { message: "Validation failed", errors: { Email: ["Email đã được sử dụng.", "Email đã được sử dụng."], Password: ["Mật khẩu quá ngắn."] } } } }), "Email đã được sử dụng. Mật khẩu quá ngắn.");
  assert.equal(registrationApiError({ response: { data: { errors: [{ propertyName: "Email", errorMessage: "Email không hợp lệ." }] } } }), "Email không hợp lệ.");
  assert.equal(registrationApiError({ response: { data: { message: "Email này đã được sử dụng." } } }), "Email này đã được sử dụng.");
  assert.match(registrationApiError(new Error("Private server exception")), /kiểm tra kết nối/);
});
