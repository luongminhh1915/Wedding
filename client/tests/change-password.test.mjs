import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validatePasswordChange } from '../src/features/auth/components/changePasswordValidation.ts';

const valid = { currentPassword: '123456', newPassword: 'a new passphrase', confirmPassword: 'a new passphrase' };
test('legacy current passwords remain usable while new passwords must be stronger and confirmed', () => {
  assert.deepEqual(validatePasswordChange(valid), {});
  assert.ok(validatePasswordChange({ ...valid, currentPassword: '' }).currentPassword);
  assert.ok(validatePasswordChange({ ...valid, newPassword: '1234567' }).newPassword);
  assert.ok(validatePasswordChange({ ...valid, confirmPassword: 'different' }).confirmPassword);
  assert.ok(validatePasswordChange({ currentPassword: 'same password', newPassword: 'same password', confirmPassword: 'same password' }).newPassword);
});
test('password validation uses UTF8 byte limits without trimming meaningful spaces', () => {
  const atLimit = 'ắ'.repeat(24);
  assert.deepEqual(validatePasswordChange({ ...valid, newPassword: atLimit, confirmPassword: atLimit }), {});
  assert.ok(validatePasswordChange({ ...valid, newPassword: `${atLimit}a`, confirmPassword: `${atLimit}a` }).newPassword);
  assert.ok(validatePasswordChange({ ...valid, newPassword: ' '.repeat(8), confirmPassword: ' '.repeat(8) }).newPassword);
  assert.deepEqual(validatePasswordChange({ ...valid, newPassword: ' pass phrase ', confirmPassword: ' pass phrase ' }), {});
});
