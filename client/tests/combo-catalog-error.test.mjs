import test from 'node:test';
import assert from 'node:assert/strict';
import { catalogErrorMessage } from '../src/features/journey/combo/catalogError.ts';

test('missing modern catalog endpoint explains backend restart only in development', () => {
  const error = { response: { status: 404 } };
  assert.match(catalogErrorMessage(error, true), /chưa đồng bộ.*khởi động lại backend/u);
  assert.doesNotMatch(catalogErrorMessage(error), /API|backend|khởi động/u);
  assert.doesNotMatch(catalogErrorMessage(error, false), /API|backend|khởi động/u);
  assert.match(catalogErrorMessage(error, false), /Vui lòng thử lại/u);
});

test('network and other failures remain retryable without claiming the endpoint is old', () => {
  for (const error of [undefined, null, 'failure', {}, { response: null }, { response: 'failure' },
    { response: {} }, { response: { status: 400 } }, { response: { status: 401 } },
    { response: { status: 500 } }, { response: { status: '404' } }]) {
    assert.equal(catalogErrorMessage(error, true), 'Kiểm tra kết nối với API rồi thử lại.');
    assert.equal(catalogErrorMessage(error, false), 'Kiểm tra kết nối với API rồi thử lại.');
  }
});

test('raw server messages, stack traces, URLs and private values are never exposed', () => {
  for (const status of [404, 500, undefined]) {
    const error = { message: 'private-connection-string', stack: 'private-file-path',
      response: { status, data: { message: '<script>private-token</script>', detail: 'private-customer' } } };
    for (const development of [true, false]) {
      assert.doesNotMatch(catalogErrorMessage(error, development), /private|<script>/u);
    }
  }
});
