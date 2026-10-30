// Test-only static render of actual components: no login, API, token or database access.
import { readFile, access, writeFile, mkdir } from 'node:fs/promises';
import { resolve, dirname, extname } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import ts from 'typescript';

const directory = dirname(fileURLToPath(import.meta.url));
const client = resolve(directory, '../..');
const state = globalThis[Symbol.for('wedding.qa.actor-navbar')] = {};
const dataUrl = text => 'data:text/javascript;base64,' + Buffer.from(text).toString('base64');
const mocks = dataUrl(`
 const state = globalThis[Symbol.for('wedding.qa.actor-navbar')];
 export const useAuthStore = (selector) => selector ? selector(state.auth) : state.auth;
 useAuthStore.getState = () => state.auth;
 export const useIntentStore = { getState: () => ({ remember: () => {} }) };
 export const useComboCartStore = selector => selector({ carts: {} });
 export const useNotifications = () => ({ data: state.page, isFetching: false, isPending: false, isError: false });
 export const useReadNotifications = () => ({ isPending: false, isError: false, mutate: () => {}, mutateAsync: async () => {} });
 export default () => null;
`);
const mockedFiles = new Set([
 'src/store/authStore', 'src/store/intentStore', 'src/store/comboCartStore',
 'src/features/notifications/notificationApi', 'src/features/auth/components/AuthModal',
]);
const cache = new Map();
async function load(file) {
 if (cache.has(file)) return cache.get(file);
 let js = ts.transpileModule(await readFile(file, 'utf8'), { compilerOptions: {
   target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
 } }).outputText.replace(/^import\s+["'][^"']+\.css["'];?\s*$/gm, '');
 const dependencies = [...new Set([...js.matchAll(/from ["']([^"']+)["']/g)].map(match => match[1]))];
 for (const dependency of dependencies) {
   let target;
   if (!dependency.startsWith('.')) target = import.meta.resolve(dependency);
   else {
     const candidate = resolve(dirname(file), dependency);
     const relative = candidate.slice(client.length + 1).replaceAll('\\', '/').replace(/\.(tsx?|m?js)$/, '');
     if (mockedFiles.has(relative)) target = mocks;
     else {
       let found;
       for (const suffix of extname(candidate) ? [''] : ['.tsx', '.ts', '.mjs']) {
         try { await access(candidate + suffix); found = candidate + suffix; break; } catch {}
       }
       if (!found) throw new Error('Unmapped component dependency: ' + dependency);
       target = await load(found);
     }
   }
   for (const quote of ['"', "'"]) js = js.replaceAll('from ' + quote + dependency + quote, 'from ' + JSON.stringify(target));
 }
 const url = dataUrl(js); cache.set(file, url); return url;
}
const { default: Layout } = await import(await load(resolve(client, 'src/components/WeddingMatchLayout.tsx')));
const { default: Notifications } = await import(await load(resolve(client, 'src/features/notifications/NotificationsPage.tsx')));
await mkdir(directory, { recursive: true });
for (const role of ['Customer', 'VendorOwner', 'Moderator', 'Finance', 'CustomerCare', 'SuperAdmin']) {
 state.auth = { user: { id: 'synthetic-qa', fullName: 'QA ' + role, vendorBrandName: role === 'VendorOwner' ? 'QA Wedding Studio' : null, role }, token: 'not-an-auth-token', validated: true, logout: () => {} };
 state.page = { items: [
   { id: 'qa-1', message: 'Gói dịch vụ cưới đã được duyệt. Bạn có thể xem lại nội dung trong không gian của mình.', createdAt: '2026-10-21T12:00:00Z', readAt: '2026-10-21T13:00:00Z', targetPath: null },
   { id: 'qa-2', message: 'Nội dung kiểm tra dài: ' + 'Thông tin dịch vụ và lịch trình được giữ nguyên. '.repeat(9), createdAt: '2026-10-21T11:00:00Z', readAt: null, targetPath: null },
 ], page: 1, pageSize: 8, totalCount: 2, totalPages: 1, unreadCount: 1 };
 const tree = createElement(MemoryRouter, { initialEntries: ['/notifications'] }, createElement(Routes, null,
   createElement(Route, { element: createElement(Layout) }, createElement(Route, { path: '/notifications', element: createElement(Notifications) }))));
 const html = '<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>QA ' + role + ' — static component preview</title><link rel="stylesheet" href="/src/index.css?direct"><link rel="stylesheet" href="/src/styles/combo-builder.css?direct"><link rel="stylesheet" href="/src/components/pagination.css?direct"><link rel="stylesheet" href="/src/features/notifications/notifications.css?direct"></head><body>' + renderToStaticMarkup(tree) + '</body></html>';
 await writeFile(resolve(directory, role + '.html'), html);
}
console.log('Generated 6 synthetic static component previews; no API/auth/DB requests.');
