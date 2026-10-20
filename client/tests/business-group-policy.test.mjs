import { test } from "node:test";
import assert from "node:assert/strict";
import { commissionPercent, policyAcceptedTime, readBusinessGroups, readVendorBusinessGroupPolicy } from "../src/features/vendor-portal/businessGroupPolicy.ts";

const group = { id: "from-server", code: "server-code", name: "Nhóm từ chính sách", description: "Mô tả được quản trị viên cấu hình", defaultCommissionRate: .075, requiresCommissionBasisApproval: true, policyVersion: "server-version" };

test('policy acceptance time after SQL reload is interpreted as UTC, not the browser timezone', () => {
  const expected = new Date('2026-10-19T00:44:01Z').toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
  for (const timestamp of ['2026-10-19T00:44:01', '2026-10-19T00:44:01Z', '2026-10-19T07:44:01+07:00'])
    assert.equal(policyAcceptedTime(timestamp), expected);
  assert.equal(policyAcceptedTime('not-a-date'), 'Chưa có thông tin');
});

test("business groups display server names, rates and basis requirements without a local default list", () => {
  assert.deepEqual(readBusinessGroups([group]), [group]);
  assert.equal(commissionPercent(group.defaultCommissionRate), "7,5%");
  assert.equal(readBusinessGroups([{ ...group, defaultCommissionRate: .046 }])[0].defaultCommissionRate, .046);
  assert.deepEqual(readBusinessGroups([]), []);
});

test("malformed or duplicate group policy fails closed rather than fabricating fallback rates", () => {
  for (const value of [null, {}, [group, group], [null], [{ ...group, id: "" }], [{ ...group, policyVersion: "" }], [{ ...group, defaultCommissionRate: "0.08" }], [{ ...group, defaultCommissionRate: Number.NaN }], [{ ...group, defaultCommissionRate: 8 }], [{ ...group, requiresCommissionBasisApproval: undefined }]])
    assert.throws(() => readBusinessGroups(value), /chính sách/);
});

test("legacy vendor policy preserves its stored rate and absent membership without guessing groups", () => {
  const policy = { groups: [], usesLegacyCommissionRate: true, legacyCommissionRate: .08, policyAcceptedAt: null, policyVersion: null };
  assert.deepEqual(readVendorBusinessGroupPolicy(policy), policy);
  assert.deepEqual(readVendorBusinessGroupPolicy({ ...policy, legacyCommissionRate: null }).groups, []);
});

test("new vendor policy exposes actual membership and acknowledgment data independently of legacy rate", () => {
  const policy = { groups: [group], usesLegacyCommissionRate: false, legacyCommissionRate: null, policyAcceptedAt: "2026-10-19T01:00:00Z", policyVersion: "server-version" };
  assert.deepEqual(readVendorBusinessGroupPolicy(policy), policy);
  assert.equal(readVendorBusinessGroupPolicy(policy).groups[0].requiresCommissionBasisApproval, true);
});

test("invalid private policy does not become an empty successful dashboard", () => {
  const policy = { groups: [group], usesLegacyCommissionRate: false, legacyCommissionRate: null, policyAcceptedAt: null, policyVersion: null };
  for (const value of [null, {}, { ...policy, usesLegacyCommissionRate: "false" }, { ...policy, legacyCommissionRate: "0.08" }, { ...policy, policyAcceptedAt: "not-a-date" }, { ...policy, groups: [{ ...group, defaultCommissionRate: -1 }] }])
    assert.throws(() => readVendorBusinessGroupPolicy(value), /chính sách/);
});
