import test from 'node:test';
import assert from 'node:assert/strict';
import { accountTab, validateDisplayName } from '../src/features/account/accountPolicy.ts';
import { JOURNEY_STAGES, journeyNext } from '../src/features/customer/journeyPolicy.ts';
import { ACTOR_ROLES, roleAwareReturnPath } from '../src/features/auth/authNavigation.ts';

test('account tabs fail safely and display name validation agrees with API boundaries', () => {
  for (const value of [null, '', 'role', 'admin', 'unknown']) assert.equal(accountTab(value), 'personal');
  for (const tab of ['personal', 'edit', 'security', 'settings']) assert.equal(accountTab(tab), tab);
  for (const name of ['', '  ', 'a'.repeat(151), 'A\nB', 'A\u007fB', 'A\u0085B']) assert.ok(validateDisplayName(name));
  for (const name of [' Nguyễn Văn An ', 'A'.repeat(150), 'Nguyễn Ánh']) assert.equal(validateDisplayName(name), null);
});

test('all actors may return to common account and notifications pages, not another portal', () => {
  for (const role of ACTOR_ROLES) {
    assert.equal(roleAwareReturnPath(role, '/account?tab=security'), '/account?tab=security');
    assert.equal(roleAwareReturnPath(role, '/notifications?page=2'), '/notifications?page=2');
  }
});

test('journey has six accessible local stages and suggests next steps without gating routes', () => {
  assert.deepEqual(JOURNEY_STAGES.map(stage => stage.id), ['overview', 'checklist', 'budget', 'discovery', 'combos', 'contracts']);
  for (const step of JOURNEY_STAGES) assert.equal(roleAwareReturnPath('Customer', step.path), step.path);
  assert.equal(journeyNext('checklist').path, '/customer/dashboard?tab=budget');
  assert.equal(journeyNext('budget').path, '/combo');
  assert.equal(journeyNext('combos').path, '/customer/dashboard?tab=contracts');
  assert.equal(journeyNext('contracts'), null);
  assert.equal(journeyNext('guests'), null);
  assert.equal(journeyNext('unknown'), null);
});

test('existing requests route next-action to follow-up; no fake overall wedding completion percentage', () => {
  assert.equal(journeyNext('overview', { leadCount: 2, contractCount: 0 }).path, '/customer/dashboard?tab=contracts');
  assert.equal(journeyNext('overview', { leadCount: 0, contractCount: 1 }).path, '/customer/dashboard?tab=contracts');
  assert.equal(journeyNext('overview', { leadCount: 0, contractCount: 0 }).path, '/customer/dashboard?tab=checklist');
});
