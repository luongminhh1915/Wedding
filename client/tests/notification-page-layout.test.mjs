import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import postcss from 'postcss';
import ts from 'typescript';

const state = globalThis[Symbol.for('wedding.tests.notification-page-layout')] = {};
const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const hooks = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.notification-page-layout')];
  export const useAuthStore = selector => selector({ user: state.user, token: 'synthetic-test-token' });
  useAuthStore.getState = () => ({ token: 'synthetic-test-token' });
  export const useNotifications = () => state.query;
  export const useReadNotifications = () => ({ isPending: false, isError: false, mutate: () => state.writes++, mutateAsync: async () => state.writes++ });
  export const useResponsivePageSize = () => 8;
  export const scrollToList = () => {};
  export const Pagination = () => null;
`);
const pageSource = await readFile(new URL('../src/features/notifications/NotificationsPage.tsx', import.meta.url), 'utf8');
let javascript = ts.transpileModule(pageSource, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
} }).outputText.replace(/^import\s+["'][^"']+\.css["'];?\s*$/gm, '');
const dependencies = {
  react: import.meta.resolve('react'), 'react/jsx-runtime': import.meta.resolve('react/jsx-runtime'),
  'react-router-dom': import.meta.resolve('react-router-dom'), 'lucide-react': import.meta.resolve('lucide-react'),
  '../../store/authStore': hooks, '../../components/Pagination': hooks, './notificationApi': hooks,
  './notificationModel': new URL('../src/features/notifications/notificationModel.ts', import.meta.url).href,
};
for (const [dependency, target] of Object.entries(dependencies))
  for (const quote of ['"', "'"])
    javascript = javascript.replaceAll('from ' + quote + dependency + quote, 'from "' + target + '"');
assert.doesNotMatch(javascript, /from\s+["']\.\.?\//, 'All actual page runtime dependencies are mapped');
const { default: NotificationsPage } = await import(moduleUrl(javascript));
const css = postcss.parse(await readFile(new URL('../src/features/notifications/notifications.css', import.meta.url), 'utf8'));
const declarations = rule => Object.fromEntries(rule.nodes.filter(node => node.type === 'decl').map(node => [node.prop, node.value]));
const rootRule = selector => css.nodes.find(node => node.type === 'rule' && node.selector === selector);
function render(role, extraQuery = {}, route = '/notifications') {
  state.user = { role }; state.writes = 0;
  state.query = { isPending: false, isError: false, isFetching: false,
    data: { items: [], page: 1, pageSize: 8, totalCount: 0, totalPages: 1, unreadCount: 0 }, ...extraQuery };
  return renderToStaticMarkup(createElement(MemoryRouter, { initialEntries: [route] }, createElement(NotificationsPage)));
}

test('notification main owns centering and viewport gutters, independent of global container utility', () => {
  assert.match(render('VendorOwner'), /<main class="wm-notifications"/);
  const main = declarations(rootRule('.wm-notifications'));
  assert.equal(main['margin-inline'], 'auto');
  assert.equal(main.width, 'min(1050px,calc(100% - 40px))');
  assert.equal(main['box-sizing'], 'border-box');
  const mobile = css.nodes.find(node => node.type === 'atrule' && node.name === 'media' && /max-width:600px/.test(node.params));
  assert.equal(declarations(mobile.nodes.find(node => node.selector === '.wm-notifications')).width, 'calc(100% - 32px)');
  assert.equal(declarations(rootRule('.wm-notifications-list p'))['overflow-wrap'], 'anywhere');
  assert.equal(declarations(rootRule('.wm-notifications-panel>.wm-pagination')).padding, '20px 24px');
});

test('centered notification page keeps the correct workspace link for every supported actor', () => {
  for (const [role, target] of [['Customer', '/customer/dashboard'], ['VendorOwner', '/vendor/dashboard'],
    ['Moderator', '/admin/dashboard'], ['Finance', '/admin/dashboard'], ['CustomerCare', '/admin/dashboard'], ['SuperAdmin', '/admin/dashboard']]) {
    const html = render(role);
    assert.match(html, /aria-label="Danh sách thông báo"/);
    assert.ok(html.includes(`href="${target}"`), role);
    assert.match(html, /Chưa có thông báo nào/);
    assert.equal(state.writes, 0, 'Layout/render must not mark notifications read');
  }
});

test('loading, retryable errors and unread empty states remain distinct after layout change', () => {
  const pending = render('Customer', { isPending: true, data: undefined });
  assert.match(pending, /Đang tải thông báo của bạn/);
  assert.doesNotMatch(pending, /Chưa có thông báo nào/);
  const failed = render('Finance', { isError: true, data: undefined });
  assert.match(failed, /role="alert"/); assert.match(failed, /Thử lại/);
  assert.doesNotMatch(failed, /Chưa có thông báo nào/);
  const unread = render('VendorOwner', {}, '/notifications?filter=unread');
  assert.match(unread, /Bạn đã đọc hết thông báo/);
  assert.equal(state.writes, 0);
});

test('long notification content, status and role-safe target still render without a mutation', () => {
  const html = render('VendorOwner', { data: {
    items: [{ id: 'synthetic-notification', message: 'Synthetic notification '.repeat(40), createdAt: '2026-10-21T12:00:00Z', readAt: null,
      targetPath: '/vendor/dashboard?tab=combos' }], page: 1, pageSize: 8, totalCount: 1, totalPages: 1, unreadCount: 1,
  } });
  assert.match(html, /is-unread/); assert.match(html, /Đánh dấu đã đọc/);
  assert.match(html, /Synthetic notification/); assert.match(html, /1 thông báo/);
  assert.equal(state.writes, 0);
});
