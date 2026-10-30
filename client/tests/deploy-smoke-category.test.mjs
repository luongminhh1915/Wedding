import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// Static smoke-contract regression; actual HTTPS/SQL execution is a separate release gate.
const smoke = await readFile(new URL('../../tools/Verify-AccountSecurity.ps1', import.meta.url), 'utf8');
const policy = await readFile(new URL('../../src/Wedding.Domain/Entities/BusinessGroup.cs', import.meta.url), 'utf8');

test('deployment smoke registers vendors without choosing groups or financial rates', () => {
  const signups = [...smoke.matchAll(/\$(?:vendor|otherVendor) = Invoke-QAApi '\/api\/auth\/register-vendor' @\{([\s\S]*?)\n\}/g)];
  assert.equal(signups.length, 2);
  for (const [, payload] of signups) {
    assert.match(payload, /partnershipPolicyAccepted = \$true/);
    assert.doesNotMatch(payload, /businessGroupIds|commissionRate|policyVersion/i);
  }
  assert.match(policy, /public const string ServiceCategoryRegistrationVersion = "[^"]+";/);
  assert.match(smoke, /Get-Content -LiteralPath \$policySource -Raw/);
  assert.match(smoke, /public const string ServiceCategoryRegistrationVersion/);
  assert.match(smoke, /\$policy\.policyVersion -cne \$registrationVersion/);
  assert.match(smoke, /\$policy\.usesLegacyCommissionRate -or \$null -ne \$policy\.legacyCommissionRate/);
  assert.match(smoke, /@\(\$policy\.groups\)\.Count -ne 0/);
  assert.doesNotMatch(smoke, /\$policy\.groups\[0\]/);
});

test('deployment smoke verifies open canonical categories, not fabricated signup memberships', () => {
  assert.match(smoke, /\$BaseUrl \+ '\/api\/categories\/my'/);
  assert.match(smoke, /\$eligibility\.requiresMembershipReview/);
  assert.match(smoke, /@\(\$eligibility\.categories\)\.Count -ne 8/);
  assert.match(smoke, /@\(\$eligibility\.businessGroupIds\)\.Count -ne 0/);
  assert.match(smoke, /@\(\$eligibility\.categories\.businessGroupId \| Sort-Object -Unique\)\.Count -ne 5/);
  for (const field of ['businessGroupId', 'businessGroupName', 'defaultCommissionRate', 'requiresCommissionBasisApproval'])
    assert.match(smoke, new RegExp('\\$category\\.' + field + ' -c?ne \\$canonical\\[0\\]\\.' + field));
});

test('deployment smoke retains disposable target, role and private draft safety checks', () => {
  for (const preserved of [
    "if ($BaseUrl -cne 'https://127.0.0.1:5011')",
    "if ($DatabaseName -cnotmatch '^WeddingPlanTest_Study_[a-f0-9]{32}$')",
    '$listener[0].OwningProcess -ne $ApiProcessId',
    "$expected = if ($deniedToken) { 403 } else { 401 }",
    "if ($denied.StatusCode -ne $expected)",
    "if ($deniedCategories.StatusCode -ne $expected)",
    "Invoke-QAListingSmoke $privateListingPath '' 404",
    'Invoke-QAListingSmoke $privateListingPath $customer.token 404',
    'Invoke-QAListingSmoke $privateListingPath $otherVendor.token 404',
  ]) assert.ok(smoke.includes(preserved), preserved);
});
