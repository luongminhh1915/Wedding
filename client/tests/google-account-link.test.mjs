import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

const state = globalThis[Symbol.for('wedding.tests.google-account-link')] = {
  role: 'Customer', token: 'synthetic-session', sessionRevision: 3,
  providers: { googleClientId: 'public-client-id' }, linked: false,
};
const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const authStore = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.google-account-link')];
  export const useAuthStore = selector => selector({ token: state.token, sessionRevision: state.sessionRevision, user: { role: state.role, id: 'synthetic-user' } });
  useAuthStore.getState = () => ({ token: state.token, sessionRevision: state.sessionRevision, user: { role: state.role, id: 'synthetic-user' } });
`);
const reactQuery = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.google-account-link')];
  export const useQuery = ({ queryKey }) => queryKey[0] === 'auth-providers'
    ? { data: state.providers, isLoading: false, isError: false, refetch: async () => ({}) }
    : { data: { linked: state.linked }, isLoading: false, isError: false, refetch: async () => ({}) };
  export const useQueryClient = () => ({ invalidateQueries: async () => ({}) });
`);
const api = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.google-account-link')];
  export const apiError = () => 'API error';
  export const authApi = { providers: async () => state.providers, googleLinkStatus: async () => ({ linked: state.linked }), linkGoogle: async () => ({}) };
`);
const source = await readFile(new URL('../src/features/account/GoogleAccountLink.tsx', import.meta.url), 'utf8');
let javascript = ts.transpileModule(source, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
} }).outputText;
for (const [specifier, target] of Object.entries({
  react: import.meta.resolve('react'),
  'react/jsx-runtime': import.meta.resolve('react/jsx-runtime'),
  '../../store/authStore': authStore, '../journey/api': api,
  '@tanstack/react-query': reactQuery, 'lucide-react': import.meta.resolve('lucide-react'),
})) for (const quote of ['"', "'"]) javascript = javascript.replaceAll(`from ${quote}${specifier}${quote}`, `from "${target}"`);
assert.doesNotMatch(javascript, /from\s+["']\.\.?\//, 'component runtime imports must be mapped');
const { default: GoogleAccountLink } = await import(moduleUrl(javascript));
function render({ role = 'Customer', clientId = 'public-client-id', linked = false } = {}) {
  Object.assign(state, { role, providers: clientId ? { googleClientId: clientId } : {}, linked });
  return renderToStaticMarkup(createElement(GoogleAccountLink));
}

test('Google account link is only rendered for customers and does not expose the action to staff/vendor actors', () => {
  for (const role of ['VendorOwner', 'Moderator', 'Finance', 'CustomerCare', 'SuperAdmin']) {
    assert.equal(render({ role }), '', role);
  }
});

test('customer sees configuration state without a fake Google action when provider is disabled', () => {
  const html = render({ clientId: '' });
  assert.match(html, /chưa được cấu hình/i);
  assert.doesNotMatch(html, /Liên kết tài khoản với Google/);
});

test('linked customer sees current link status instead of another link button', () => {
  const html = render({ linked: true });
  assert.match(html, /đã liên kết với Google/i);
  assert.doesNotMatch(html, /aria-label="Liên kết tài khoản với Google"/);
});

test('unlinked customer with configured provider receives the actual GIS link mount point', () => {
  const html = render();
  assert.match(html, /aria-label="Liên kết tài khoản với Google"/);
  assert.match(html, /Email Google phải khớp email tài khoản/);
});
