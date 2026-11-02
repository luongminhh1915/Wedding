import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

// Render the actual pages. This controlled hook adapter lets us invoke their
// actual event handlers across query changes; it is not browser/effect QA.
const key = Symbol.for('wedding.tests.vendor-quote-entry');
const state = globalThis[key] = {};
const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const hooks = moduleUrl(`
  import * as React from ${JSON.stringify(import.meta.resolve('react'))};
  const state = globalThis[Symbol.for('wedding.tests.vendor-quote-entry')];
  export default React;
  export const useState = initial => {
    const index = state.stateCursor++;
    if (!(index in state.values)) state.values[index] = typeof initial === 'function' ? initial() : initial;
    return [state.values[index], next => { state.values[index] = typeof next === 'function' ? next(state.values[index]) : next; }];
  };
  export const useRef = initial => {
    const index = state.refCursor++;
    return state.refs[index] ??= { current: initial };
  };
  export const useEffect = () => {};
  export const useLayoutEffect = callback => { state.layoutEffects.push(callback); };
  export const useQuery = () => state.leads;
  export const useFinancialQuotes = () => state.quotes;
  export const useSendFinancialQuote = () => state.send;
  export const useNotificationFocus = () => ({ missing: false });
  export const apiError = error => error?.message ?? 'Read failed';
  export const useChatRoom = () => state.chat;
  export const MoneyInput = props => React.createElement('input', { 'aria-label': props['aria-label'], value: props.value ?? '', readOnly: true });
`);
const runtime = moduleUrl(`
  import { jsx as baseJsx, jsxs as baseJsxs, Fragment } from ${JSON.stringify(import.meta.resolve('react/jsx-runtime'))};
  const state = globalThis[Symbol.for('wedding.tests.vendor-quote-entry')];
  const capture = original => (type, props, key) => {
    state.nodes.push({ type, props });
    return original(type, props, key);
  };
  export const jsx = capture(baseJsx), jsxs = capture(baseJsxs); export { Fragment };
`);
async function componentUrl(path, aliases = {}) {
  const source = await readFile(new URL(path, import.meta.url), 'utf8');
  let output = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  for (const [dependency, target] of Object.entries({
    react: hooks, 'react/jsx-runtime': runtime, 'lucide-react': import.meta.resolve('lucide-react'),
    '@tanstack/react-query': hooks, ...aliases,
  })) for (const quote of ['"', "'"]) output = output.replaceAll('from ' + quote + dependency + quote, 'from "' + target + '"');
  assert.doesNotMatch(output, /from\s+["']\.\.?\//, 'Unmapped dependency in ' + path);
  return moduleUrl(output);
}
const ui = await componentUrl('../src/features/journey/ui.tsx');
const { VendorQuotes } = await import(await componentUrl('../src/features/journey/vendor/VendorQuotes.tsx', {
  '../../../services/axiosClient': moduleUrl('export default {};'), '../api': hooks, '../financeApi': hooks,
  '../ui': ui, '../format': new URL('../src/features/journey/format.ts', import.meta.url).href,
  '../../notifications/useNotificationFocus': hooks, '../../../components/MoneyInput': hooks,
}));
const { ChatRoomModal } = await import(await componentUrl('../src/features/chat/components/ChatRoomModal.tsx', {
  '../hooks/useChatRoom': hooks, '../../../components/MoneyInput': hooks,
}));
const first = { id: 'lead-one', status: 'Accepted', customerName: 'Synthetic Customer', listingTitle: 'Original service', estimatedBudget: 1000000 };
function reset() {
  state.values = []; state.refs = []; state.nodes = []; state.sent = []; state.retries = 0;
  state.leads = { data: [first], isPending: false, isError: false, isFetching: false, refetch: () => { state.retries++; } };
  state.quotes = { data: [], isPending: false, isError: false, refetch: () => {} };
  state.send = { isPending: false, isError: false, mutate: (body, options) => { state.sent.push({ body, options }); } };
  state.chat = { messages: [], isLoading: false, isConnected: true, error: null, refetch: () => {}, sendMessage: async body => state.sent.push(body) };
}
function render(component = VendorQuotes, props) {
  state.stateCursor = 0; state.refCursor = 0; state.nodes = []; state.layoutEffects = [];
  const markup = renderToStaticMarkup(createElement(component, props));
  // Commit the page's layout synchronization before invoking its controls.
  state.layoutEffects.forEach(callback => callback());
  return markup;
}
function control(predicate) {
  const result = state.nodes.find(predicate)?.props;
  assert.ok(result, 'Expected actual page control');
  return result;
}
function choose(id = first.id) {
  render();
  control(node => node.type === 'select' && node.props.value === '').onChange({ target: { value: id } });
  return render();
}
const submit = () => control(node => node.type === 'button' && node.props.onClick?.name === 'submit');
function chatProps(isVendorUser = false) {
  return { isOpen: true, onClose: () => {}, leadId: first.id, partnerName: 'Synthetic Partner', isVendorUser };
}

test('vendor quote entry does not call initial loading an empty inbox', () => {
  reset(); Object.assign(state.leads, { data: undefined, isPending: true, isFetching: true });
  const html = render();
  assert.match(html, /Đang tải yêu cầu tư vấn/);
  assert.doesNotMatch(html, /Chưa có yêu cầu nào đã tiếp nhận/);
  assert.equal(state.sent.length, 0);
});
test('vendor quote entry distinguishes failed read from true empty and retry is read-only', () => {
  reset(); Object.assign(state.leads, { data: undefined, isError: true });
  const html = render();
  assert.match(html, /Chưa tải được yêu cầu tư vấn/);
  assert.doesNotMatch(html, /Chưa có yêu cầu nào đã tiếp nhận/);
  control(node => node.type === 'button' && node.props.children === 'Tải lại yêu cầu').onClick();
  assert.equal(state.retries, 1); assert.equal(state.sent.length, 0);
  Object.assign(state.leads, { data: [], isError: false });
  assert.match(render(), /Chưa có yêu cầu nào đã tiếp nhận/);
});
test('read failure preserves selected draft and blocks even an older send handler', () => {
  reset(); choose(); const oldSend = submit().onClick;
  Object.assign(state.leads, { isError: true });
  const html = render();
  assert.match(html, /Bản nháp đang nhập được giữ nguyên/);
  assert.match(html, /value="Original service"/);
  assert.equal(control(node => node.type === 'fieldset').disabled, true);
  assert.equal(submit().disabled, true);
  oldSend(); assert.equal(state.sent.length, 0);
});
test('refetch with cached leads holds sending and picking until query is current', () => {
  reset(); choose(); const oldSend = submit().onClick;
  state.leads.isFetching = true;
  assert.match(render(), /Đang cập nhật yêu cầu tư vấn/);
  assert.equal(submit().disabled, true);
  const picker = control(node => node.type === 'select' && node.props.value === first.id);
  assert.equal(picker.disabled, true);
  oldSend(); assert.equal(state.sent.length, 0);
  state.leads.isFetching = false; render(); submit().onClick();
  assert.equal(state.sent.length, 1);
});
test('successful refresh invalidates a removed or closed selected lead without erasing draft', () => {
  for (const refreshed of [[], [{ ...first, status: 'Cancelled' }], [{ ...first, status: 'Contracted' }]]) {
    reset(); choose(); const oldSend = submit().onClick;
    state.leads.data = refreshed;
    const html = render();
    assert.match(html, /Yêu cầu đã chọn không còn nhận báo giá/);
    assert.match(html, /value="Original service"/);
    assert.equal(submit().disabled, true); oldSend(); assert.equal(state.sent.length, 0);
  }
});
test('choosing another eligible lead cannot send a stale previous-selection callback', () => {
  reset(); state.leads.data.push({ ...first, id: 'lead-two', listingTitle: 'Second service' });
  choose(); const previousSubmit = submit().onClick;
  control(node => node.type === 'select' && node.props.value === first.id).onChange({ target: { value: 'lead-two' } });
  render(); previousSubmit(); assert.equal(state.sent.length, 0);
  submit().onClick(); assert.equal(state.sent[0].body.leadId, 'lead-two');
  assert.equal(state.sent[0].body.items[0].name, 'Second service');
});
test('send locks synchronously against duplicate clicks and sends unchanged formal quote fields', () => {
  reset(); choose(); const send = submit().onClick;
  send(); send(); assert.equal(state.sent.length, 1);
  assert.deepEqual(state.sent[0].body, { leadId: first.id, items: [{ name: first.listingTitle, quantity: 1, unitPrice: first.estimatedBudget }], depositAmount: 300000, terms: 'Cọc 30% khi ký, thanh toán phần còn lại trước ngày cưới 7 ngày.', validDays: 7, vatRate: 0 });
  state.send.isPending = true; render();
  assert.equal(control(node => node.type === 'fieldset').disabled, true);
  assert.equal(control(node => node.type === 'select' && node.props.value === first.id).disabled, true);
  state.sent[0].options.onSuccess(); state.sent[0].options.onSettled(); state.send.isPending = false;
  render(); assert.equal(state.nodes.some(node => node.type === 'fieldset'), false);
});
test('failed mutation unlocks retry and retains entered quote', () => {
  reset(); choose(); submit().onClick();
  state.sent[0].options.onSettled(); Object.assign(state.send, { isError: true, error: new Error('Synthetic send failure') });
  assert.match(render(), /Synthetic send failure/);
  assert.match(render(), /value="Original service"/);
  submit().onClick(); assert.equal(state.sent.length, 2);
});
test('chat price cards are estimates for both actors, without fake formal or discount claims', () => {
  for (const isVendorUser of [false, true]) {
    reset(); state.chat.messages = [{ id: 'message-one', senderName: 'Synthetic Partner', isFromCustomer: false, createdAt: '2026-10-28T00:00:00Z', isQuote: true, quoteAmount: 1000000, quoteDescription: 'Discussed scope' }];
    const html = render(ChatRoomModal, chatProps(isVendorUser));
    assert.match(html, /Chi phí tham khảo/);
    assert.match(html, /Chưa phải báo giá để xác nhận/);
    assert.doesNotMatch(html, /Báo Giá Chính Thức|Đã bao gồm chiết khấu/);
  }
});
test('vendor chat estimate form explains formal quote step and keeps existing message API fields', async () => {
  reset(); render(ChatRoomModal, chatProps(true));
  control(node => node.type === 'button' && node.props.title === 'Gửi chi phí tham khảo').onClick();
  assert.match(render(ChatRoomModal, chatProps(true)), /hãy gửi hồ sơ riêng trong mục Báo giá/);
  control(node => node.props['aria-label'] === 'Chi phí tham khảo (VNĐ)' && node.props.onValueChange).onValueChange(2000000);
  render(ChatRoomModal, chatProps(true));
  await control(node => node.type === 'form' && node.props.onSubmit.name === 'handleSendQuote').onSubmit({ preventDefault() {} });
  assert.deepEqual(state.sent, [{ content: 'Chi phí tham khảo khi trao đổi: 2.000.000 đ', isQuote: true, quoteAmount: 2000000, quoteDescription: undefined }]);
});
