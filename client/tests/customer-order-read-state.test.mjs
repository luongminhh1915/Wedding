import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import ts from 'typescript';

// Render the actual pages, cards, rows and error component. Only data hooks,
// route pagination and unrelated closed-dialog children are controlled.
const stateKey = Symbol.for('wedding.tests.customer-order-read-state');
const state = globalThis[stateKey] = {};
const module = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const jsxRuntime = module([
  'import { jsx as baseJsx, jsxs as baseJsxs, Fragment } from ' + JSON.stringify(import.meta.resolve('react/jsx-runtime')) + ';',
  'const state = globalThis[Symbol.for("wedding.tests.customer-order-read-state")];',
  'const wrap = original => (type, props, key) => { if (type === "button") state.buttons.push(props); return original(type, props, key); };',
  'export const jsx = wrap(baseJsx), jsxs = wrap(baseJsxs); export { Fragment };',
].join('\n'));
const hooks = module([
  'import { createElement } from ' + JSON.stringify(import.meta.resolve('react')) + ';',
  'const state = globalThis[Symbol.for("wedding.tests.customer-order-read-state")];',
  'const mutation = () => ({ isError: false, error: null, isPending: false, mutate: () => { state.writes++; }, mutateAsync: async () => { state.writes++; }, reset: () => {} });',
  'export const apiError = error => error?.response?.data?.message || error?.message || "QA API unavailable";',
  'export const useCustomerOrderPage = () => state.page, useSavedCombosPage = () => state.page, useCustomerOrderJourney = () => state.journey;',
  'export const useCustomerOrderAction = mutation, useRespondQuote = mutation, useMutation = mutation;',
  'export const useQueryClient = () => ({ invalidateQueries: () => { state.invalidations++; } });',
  'export const useCustomerContracts = () => ({ isConfirming: false, isRejecting: false, confirmContract: async () => { state.writes++; }, rejectContract: async () => { state.writes++; } });',
  'export const useOrderPage = () => ({ page: 1, pageSize: 8, filter: "all", focusId: undefined, setFilter: () => {}, changePage: () => {}, listRef: null });',
  'export const useNotificationFocus = () => ({ missing: false, clearFocus: () => {} });',
  'export const Pagination = () => createElement("nav", { "data-testid": "pagination" }, "QA pagination");',
  'export default { post: async () => { state.writes++; } };',
].join('\n'));
const leaves = module([
  'export const ChatRoomModal = () => null, ComboDialog = () => null, OrderGallery = () => null, CustomerOrderActionDialog = () => null, CustomerOrderRequestHistory = () => null, ContractCategorySummary = () => null, PrivateServiceAgreementPanel = () => null;',
  'export const unitName = { Package: "gói" }; export const comboEditIntent = order => ({ draft: order });',
].join('\n'));
async function moduleUrl(file, aliases = {}) {
  const source = await readFile(new URL(file, import.meta.url), 'utf8');
  let javascript = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText.replace(/^import\s+["'][^"']+\.css["'];?\s*$/gm, '');
  const dependencies = {
    react: import.meta.resolve('react'), 'react/jsx-runtime': jsxRuntime,
    'react-router-dom': import.meta.resolve('react-router-dom'), 'lucide-react': import.meta.resolve('lucide-react'),
    '@tanstack/react-query': hooks, ...aliases,
  };
  for (const [dependency, target] of Object.entries(dependencies))
    for (const quote of ['"', "'"]) javascript = javascript.replaceAll('from ' + quote + dependency + quote, 'from "' + target + '"');
  assert.doesNotMatch(javascript, /from\s+["']\.\.?\//, 'Unmapped runtime dependency: ' + file);
  return module(javascript);
}
const local = file => new URL(file, import.meta.url).href;
const media = local('../src/features/journey/combo/comboMedia.ts');
const photo = await moduleUrl('../src/features/journey/combo/ComboPhoto.tsx', { './comboMedia': media });
const orderUi = await moduleUrl('../src/features/journey/customerOrders/OrderUi.tsx', {
  '../combo/ComboPhoto': photo, '../combo/ComboDialog': leaves,
});
const orderMedia = await moduleUrl('../src/features/journey/customerOrders/orderMedia.ts', { '../combo/comboMedia': media });
const readError = await moduleUrl('../src/features/journey/customerOrders/OrderReadError.tsx', { '../api': hooks });
const paths = {
  format: local('../src/features/journey/format.ts'),
  presentation: local('../src/features/journey/customerOrders/orderPresentation.ts'),
  comboModel: local('../src/features/journey/customerOrders/comboOrderModel.ts'),
};
const { CustomerLeads } = await import(await moduleUrl('../src/features/customer/components/CustomerLeads.tsx', {
  '../../../services/axiosClient': hooks, '../../chat/components/ChatRoomModal': leaves,
  '../../journey/api': hooks, '../../journey/format': paths.format,
  '../../journey/customerOrders/customerOrderApi': hooks, '../../journey/customerOrders/OrderUi': orderUi,
  '../../journey/customerOrders/OrderGallery': leaves, '../../journey/customerOrders/orderMedia': orderMedia,
  '../../journey/customerOrders/orderPresentation': paths.presentation,
  '../../journey/customerOrders/OrderReadError': readError,
  './CustomerOrderActionDialog': leaves, '../../../components/Pagination': hooks,
  '../../journey/customerOrders/useOrderPage': hooks, '../../notifications/useNotificationFocus': hooks,
}));
const { MyQuotes } = await import(await moduleUrl('../src/features/journey/quotes/MyQuotes.tsx', {
  '../api': hooks, '../format': paths.format, '../customerOrders/customerOrderApi': hooks,
  '../customerOrders/OrderUi': orderUi, '../customerOrders/OrderGallery': leaves,
  '../customerOrders/orderMedia': orderMedia, '../customerOrders/orderPresentation': paths.presentation,
  '../customerOrders/OrderReadError': readError,
  '../../customer/components/CustomerOrderActionDialog': leaves, '../../../components/Pagination': hooks,
  '../customerOrders/useOrderPage': hooks, '../../notifications/useNotificationFocus': hooks,
}));
const { MyContracts } = await import(await moduleUrl('../src/features/customer/components/MyContracts.tsx', {
  '../hooks/useCustomerContracts': hooks, '../../journey/api': hooks, '../../journey/format': paths.format,
  '../../journey/combo/ComboDialog': leaves, '../../journey/combo/comboMedia': media,
  '../../journey/customerOrders/OrderUi': orderUi, '../../journey/customerOrders/OrderGallery': leaves,
  '../../journey/customerOrders/orderMedia': orderMedia, '../../journey/customerOrders/customerOrderApi': hooks,
  '../../journey/customerOrders/orderPresentation': paths.presentation,
  '../../journey/customerOrders/OrderReadError': readError,
  './CustomerOrderActionDialog': leaves, '../../../components/Pagination': hooks,
  '../../journey/customerOrders/useOrderPage': hooks, '../../notifications/useNotificationFocus': hooks,
  '../../contracts/ContractCategorySummary': leaves, '../../private-agreements/PrivateServiceAgreementPanel': leaves,
}));
const { MyCombos } = await import(await moduleUrl('../src/features/journey/combo/MyCombos.tsx', {
  '../comboApi': hooks, '../../../components/Pagination': hooks, '../customerOrders/useOrderPage': hooks,
  '../../notifications/useNotificationFocus': hooks, '../../private-agreements/PrivateServiceAgreementPanel': leaves,
  '../api': hooks, '../format': paths.format, './ComboDialog': leaves, './cartModel': leaves,
  './comboEditing': leaves, '../customerOrders/customerOrderApi': hooks,
  '../customerOrders/comboOrderModel': paths.comboModel, '../customerOrders/OrderUi': orderUi,
  '../customerOrders/OrderReadError': readError,
}));

const caps = { canEdit: true, canArchive: true, canRestore: true, canWithdraw: true,
  canRespondQuote: true, canAcceptQuote: true, canConfirm: true, canRequestChanges: true };
const row = kind => ({ id: kind + '-qa', vendorId: 'vendor-qa', vendorBrandName: 'QA SHOP', vendorName: 'QA SHOP',
  listingTitle: 'QA SERVICE', listingId: 'listing-qa', createdAt: '2026-10-01T00:00:00Z', weddingDate: '2099-12-01T00:00:00Z',
  status: kind === 'quote' ? 'Sent' : kind === 'contract' ? 'PendingVerification' : kind === 'combo' ? 'Draft' : 'Accepted',
  code: 'QA-QUOTE-OR-COMBO', version: 1, totalAmount: 1000000, total: 1000000, subtotal: 1000000,
  discountAmount: 0, depositAmount: 0, contractValue: 1000000, contractCode: 'QA-CONTRACT',
  verificationDeadline: '2099-12-01T00:00:00Z', validUntil: '2099-12-01T00:00:00Z', items: [], guestCount: 100 });
const metadata = kind => ({ id: kind + '-qa', kind, isArchived: false, requests: [], capabilities: { ...caps },
  media: { title: 'QA SERVICE', vendorName: 'QA SHOP', items: [], galleryUrls: [] } });
const query = (data, extra = {}) => ({ data, isPending: false, isLoading: false, isError: false, isSuccess: true, isFetching: false,
  error: null, refetch: async () => { state.reads++; return { data }; }, ...extra });
function reset(kind, { empty = false, pending = false, error = false, stale = false, journeyError = false, retrying = false } = {}) {
  state.writes = 0; state.reads = 0; state.invalidations = 0; state.buttons = [];
  const data = { items: empty ? [] : [row(kind)], metadata: empty ? [] : [metadata(kind)], page: 1, totalCount: empty ? 0 : 1, filter: 'all' };
  const errorValue = { response: { data: { message: 'QA API unavailable' } } };
  state.page = query((error && !stale) || pending ? undefined : data, {
    isPending: pending, isLoading: pending, isError: error, isSuccess: !pending && !error, isFetching: retrying,
    error: error ? errorValue : null,
  });
  state.journey = query(data.metadata, { isError: journeyError, isSuccess: !journeyError, isFetching: retrying,
    error: journeyError ? errorValue : null });
}
const pages = [
  { kind: 'lead', component: CustomerLeads, empty: 'Chưa có yêu cầu trong mục này', loading: 'Đang tải yêu cầu tư vấn', action: 'Trò chuyện' },
  { kind: 'quote', component: MyQuotes, empty: 'Chưa có báo giá trong mục này', loading: 'Đang tải báo giá', action: 'Chấp nhận &amp; tạo hợp' },
  { kind: 'contract', component: MyContracts, empty: 'Chưa có hợp đồng trong mục này', loading: 'Đang tải hợp đồng', action: 'Xác nhận hợp đồng' },
  { kind: 'combo', component: MyCombos, empty: 'Ngày vui bắt đầu từ một lựa chọn', loading: 'Đang mở các combo', action: 'Sửa bản nháp' },
];
const render = component => renderToStaticMarkup(createElement(MemoryRouter, null, createElement(component)));
function buttonText(value) {
  if (value == null) return '';
  if (typeof value === 'string' || typeof value === 'number') return String(value);
  if (Array.isArray(value)) return value.map(buttonText).join('');
  return buttonText(value.props?.children);
}

for (const page of pages) {
  for (const stale of [false, true]) test(page.kind + ': ' + (stale ? 'cached' : 'initial') + ' read failure is not empty/success and hides transaction actions', () => {
    reset(page.kind, { error: true, stale });
    const html = render(page.component);
    assert.match(html, /role="alert"/);
    assert.doesNotMatch(html, /class="co-empty"|class="co-card[ "]|data-testid="pagination"/);
    assert.ok(!html.includes(page.empty)); assert.ok(!html.includes(page.action));
    assert.doesNotMatch(html, /QA SHOP|QA SERVICE|QA-CONTRACT|QA-QUOTE-OR-COMBO/);
    assert.equal(state.writes, 0); assert.equal(state.invalidations, 0);
    const retry = state.buttons.find(button => /Thử lại|Tải lại/.test(buttonText(button.children)));
    assert.ok(retry, 'read error exposes retry');
    assert.equal(state.reads, 0); retry.onClick();
    assert.ok(state.reads > 0, 'retry refetches data'); assert.equal(state.writes, 0);
  });
  test(page.kind + ': loading is distinct from a successful empty result', () => {
    reset(page.kind, { pending: true });
    const loading = render(page.component);
    assert.ok(loading.includes(page.loading)); assert.doesNotMatch(loading, /class="co-empty"|role="alert"/);
    reset(page.kind, { empty: true });
    const empty = render(page.component);
    assert.ok(empty.includes(page.empty)); assert.match(empty, /class="co-empty"/); assert.doesNotMatch(empty, /role="alert"/);
    assert.equal(state.writes, 0); assert.equal(state.reads, 0);
  });
  test(page.kind + ': successful records and server-backed actions remain available', () => {
    reset(page.kind);
    const html = render(page.component);
    assert.match(html, /class="co-card[ "]/); assert.ok(html.includes(page.action), page.action);
    assert.doesNotMatch(html, /class="co-empty"|role="alert"/);
    assert.equal(state.writes, 0); assert.equal(state.reads, 0);
  });
}

test('combo metadata failure keeps a read-only snapshot, not cached edit/withdraw/archive rights', () => {
  reset('combo', { journeyError: true });
  const html = render(MyCombos);
  assert.match(html, /role="alert"/); assert.match(html, /class="co-card[ "]/);
  assert.doesNotMatch(html, /Sửa bản nháp|Hủy bản nháp|>Lưu trữ</);
  const retry = state.buttons.find(button => /Thử lại|Tải lại/.test(buttonText(button.children)));
  assert.ok(retry); retry.onClick(); assert.ok(state.reads > 0); assert.equal(state.writes, 0);
});

test('read retry is disabled while a failed request is being retried', () => {
  reset('quote', { error: true, retrying: true }); render(MyQuotes);
  const retry = state.buttons.find(button => /Thử lại|Đang.*lại/.test(buttonText(button.children)));
  assert.ok(retry); assert.equal(retry.disabled, true); assert.equal(state.writes, 0);
});

test('server can block accepting a stale-content quote without blocking rejection/requesting a replacement', () => {
  reset('quote');
  state.page.data.metadata[0].capabilities.canAcceptQuote = false;
  render(MyQuotes);
  const accept = state.buttons.find(button => /Chấp nhận.*tạo hợp/.test(buttonText(button.children)));
  const reject = state.buttons.find(button => /Chưa phù hợp/.test(buttonText(button.children)));
  assert.ok(accept); assert.equal(accept.disabled, true);
  assert.ok(reject); assert.equal(reject.disabled, false);
  assert.equal(state.writes, 0);
});

test('omitted additive accept capability preserves the older server respond permission', () => {
  reset('quote');
  delete state.page.data.metadata[0].capabilities.canAcceptQuote;
  render(MyQuotes);
  const accept = state.buttons.find(button => /Chấp nhận.*tạo hợp/.test(buttonText(button.children)));
  assert.ok(accept); assert.equal(accept.disabled, false); assert.equal(state.writes, 0);
});
