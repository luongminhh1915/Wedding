import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';
import { personalBudgetProgress } from '../src/features/customer/components/tools/personalBudgetModel.ts';

// Actual component markup and callbacks are exercised below. Data hooks are
// controlled, MoneyInput is a numeric adapter, and state slots make callback
// effects observable between SSR renders. This is not browser/React lifecycle QA.
const state = globalThis[Symbol.for('wedding.tests.personal-budget')] = {};
const module = text => 'data:text/javascript;base64,' + Buffer.from(text).toString('base64');
const hooks = module(`
  const state = globalThis[Symbol.for('wedding.tests.personal-budget')];
  export const useWeddingBudget = () => state.query;
  export const usePlannerAction = () => state.action;
  export const apiError = (error, fallback) => error?.message || fallback || 'QA API unavailable';
`);
const controlledState = module(`
  const state = globalThis[Symbol.for('wedding.tests.personal-budget')];
  export function useState(initial) {
    const index = state.cursor++;
    if (!(index in state.values)) state.values[index] = typeof initial === 'function' ? initial() : initial;
    return [state.values[index], next => {
      state.values[index] = typeof next === 'function' ? next(state.values[index]) : next;
    }];
  }
  export function useRef(initial) { const [ref] = useState(() => ({ current: initial })); return ref; }
`);
const jsx = module(`
  import { jsx as originalJsx, jsxs as originalJsxs, Fragment } from ${JSON.stringify(import.meta.resolve('react/jsx-runtime'))};
  const state = globalThis[Symbol.for('wedding.tests.personal-budget')];
  const wrap = original => (type, props, key) => {
    if (type === 'button') state.buttons.push(props);
    if (type === 'form') state.forms.push(props);
    if (type === 'input') state.fields.push(props);
    if (type === 'dialog') state.dialogs.push(props);
    return original(type, props, key);
  };
  export const jsx = wrap(originalJsx), jsxs = wrap(originalJsxs); export { Fragment };
`);
const numericInput = module(`
  import { createElement } from ${JSON.stringify(import.meta.resolve('react'))};
  const state = globalThis[Symbol.for('wedding.tests.personal-budget')];
  export function MoneyInput(props) {
    state.inputs.push(props);
    return createElement('input', { 'aria-label': props['aria-label'], value: props.value ?? '', readOnly: true });
  }
`);

const componentSource = await readFile(new URL('../src/features/customer/components/tools/WeddingBudgetPlanner.tsx', import.meta.url), 'utf8');
let dialogJs = ts.transpileModule(await readFile(new URL('../src/features/journey/combo/ComboDialog.tsx', import.meta.url), 'utf8'), { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
} }).outputText;
for (const [source, destination] of Object.entries({ react: import.meta.resolve('react'), 'react/jsx-runtime': jsx, 'lucide-react': import.meta.resolve('lucide-react') }))
  for (const quote of ['"', "'"]) dialogJs = dialogJs.replaceAll('from ' + quote + source + quote, 'from ' + JSON.stringify(destination));
const realDialog = module(dialogJs);
let componentJs = ts.transpileModule(componentSource, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
} }).outputText;
for (const [source, destination] of Object.entries({
  react: controlledState, 'react/jsx-runtime': jsx, 'lucide-react': import.meta.resolve('lucide-react'),
  '../../hooks/useWeddingTools': hooks, '../../../journey/planner/plannerApi': hooks,
  '../../../journey/api': hooks, '../../../../components/MoneyInput': numericInput,
  '../../../journey/combo/ComboDialog': realDialog,
  './personalBudgetModel': new URL('../src/features/customer/components/tools/personalBudgetModel.ts', import.meta.url).href,
})) {
  for (const quote of ['"', "'"])
    componentJs = componentJs.replaceAll('from ' + quote + source + quote, 'from ' + JSON.stringify(destination));
}
assert.doesNotMatch(componentJs, /from\s+["']\.\.?\//, 'All real component runtime dependencies must be mapped explicitly');
const { WeddingBudgetPlanner } = await import(module(componentJs));

const item = (overrides = {}) => Object.freeze({
  id: 'personal-qa-item', itemName: 'Studio ngoài hệ thống', plannedCost: 10_000_000,
  actualCost: 3_000_000, notes: 'Số tự ghi đã bao gồm tiền cọc', isPaid: true,
  owner: 'Shared', createdAt: '2026-10-21T00:00:00Z', ...overrides,
});
function reset(items = [item()], options = {}) {
  Object.assign(state, { cursor: 0, values: [], buttons: [], forms: [], inputs: [], fields: [], dialogs: [], writes: [], reads: 0, deleted: [] });
  state.query = {
    budget: { totalPlannedBudget: 30_000_000, totalActualCost: 3_000_000, remainingBudget: 27_000_000, spentPercentage: 10, items },
    isLoading: false, isError: false, isSaving: false,
    refetch: () => { state.reads++; },
    saveItem: async payload => { state.writes.push(structuredClone(payload)); },
    deleteItem: async id => { state.deleted.push(id); },
    ...options,
  };
  state.action = { isPending: false, isError: false, mutate: payload => { state.writes.push(structuredClone(payload)); } };
}
function render() {
  state.cursor = 0; state.buttons = []; state.forms = []; state.inputs = []; state.fields = []; state.dialogs = [];
  return renderToStaticMarkup(createElement(WeddingBudgetPlanner)).replaceAll('\u00a0', ' ');
}
const row = (html, name) => [...html.matchAll(/<tr\b[^>]*>[\s\S]*?<\/tr>/g)].map(match => match[0]).find(markup => markup.includes(name));

for (const [planned, spent, remaining, excess, status] of [
  [10_000_000, 0, 10_000_000, 0, 'unrecorded'],
  [10_000_000, 3_000_000, 7_000_000, 0, 'partial'],
  [10_000_000, 10_000_000, 0, 0, 'at-plan'],
  [10_000_000, 12_000_000, 0, 2_000_000, 'over-plan'],
  [0, 0, 0, 0, 'unrecorded'],
  [0, 2_000_000, 0, 2_000_000, 'over-plan'],
  [10.75, 3.25, 7.5, 0, 'partial'],
]) {
  test(`personal budget ${planned}/${spent} reports remaining/excess without payment settlement`, () => {
    const progress = personalBudgetProgress(planned, spent);
    assert.equal(progress.remaining, remaining);
    assert.equal(progress.excess, excess);
    assert.equal(progress.status, status);
    assert.equal(typeof progress.label, 'string');
    assert.ok(progress.label.trim());
    assert.doesNotMatch(progress.label, /đã thanh toán|đã đối soát|đã xác minh/i);
  });
}

test('a recorded deposit remains a partial personal amount regardless of historical IsPaid', () => {
  reset([item({ isPaid: true })]);
  const html = render(); const content = row(html, 'Studio ngoài hệ thống');
  assert.ok(content);
  assert.match(content, /3\.000\.000/);
  assert.match(content, /7\.000\.000/);
  assert.match(content, new RegExp(personalBudgetProgress(10_000_000, 3_000_000).label));
  assert.doesNotMatch(content, /đã thanh toán|đã đối soát|đã xác minh/i);
  assert.match(html, /Đã chi \/ đã cọc/i);
  assert.match(html, /Còn theo dự toán/i);
  assert.equal(state.writes.length, 0);
});

test('personal row status is derived from recorded numbers, not old settlement flags or contract linkage', () => {
  reset([
    item({ id: 'zero', itemName: 'Chưa ghi chi', actualCost: 0, isPaid: true }),
    item({ id: 'enough', itemName: 'Ghi đủ dự toán', actualCost: 10_000_000, isPaid: false, contractId: 'historical-contract' }),
    item({ id: 'over', itemName: 'Vượt dự toán', actualCost: 12_000_000, isPaid: false }),
  ]);
  const html = render();
  for (const [name, spent] of [['Chưa ghi chi', 0], ['Ghi đủ dự toán', 10_000_000], ['Vượt dự toán', 12_000_000]]) {
    const content = row(html, name); assert.ok(content);
    assert.match(content, new RegExp(personalBudgetProgress(10_000_000, spent).label));
    assert.doesNotMatch(content, /đã thanh toán|đã đối soát|đã xác minh/i);
  }
  assert.match(row(html, 'Vượt dự toán'), /2\.000\.000/);
  assert.equal(state.writes.length, 0);
});

test('summary counts cumulative recorded deposits once and leaves budget amounts unchanged', () => {
  const original = [item(), item({ id: 'decor', itemName: 'Trang trí ngoài hệ thống', actualCost: 2_000_000 })];
  const before = structuredClone(original);
  reset(original);
  const html = render();
  assert.match(html, /5\.000\.000/);
  assert.match(html, /25\.000\.000/);
  assert.doesNotMatch(html, /50\.000\.000/);
  assert.deepEqual(original, before);
  assert.equal(state.writes.length, 0);
});

test('over-budget summary remains visible instead of claiming negative money is payable', () => {
  reset([item({ actualCost: 35_000_000 })]);
  const html = render();
  assert.match(html, /5\.000\.000/);
  assert.match(html, /bội chi|vượt/i);
  assert.equal(state.writes.length, 0);
});

test('successful empty personal budget is distinct from loading and failed reads', () => {
  reset([]); const empty = render();
  assert.match(empty, /Chưa có khoản chi/);
  reset([], { isLoading: true, budget: undefined }); const loading = render();
  assert.match(loading, /Đang tải/); assert.doesNotMatch(loading, /Chưa có khoản chi/);
  reset([], { isError: true, budget: undefined }); const error = render();
  assert.match(error, /Chưa thể tải ngân sách/); assert.doesNotMatch(error, /Chưa có khoản chi/);
  const retry = state.buttons.find(button => String(button.children).includes('Thử lại'));
  assert.ok(retry); retry.onClick();
  assert.equal(state.reads, 1); assert.equal(state.writes.length, 0);
});

test('editing an external personal expense keeps the cumulative amount numeric and saves it once', async t => {
  reset([item({ contractId: 'original-contract-reference' })]); render();
  const edit = state.buttons.find(button => /Sửa khoản chi/i.test(button.title || button['aria-label'] || ''));
  assert.ok(edit); edit.onClick();
  const open = render();
  assert.match(open, /Chỉnh Sửa Khoản Chi|Chỉnh sửa khoản chi/i);
  const amount = state.inputs.find(input => /đã chi|đã cọc/i.test(input['aria-label'] || ''));
  assert.ok(amount); assert.equal(amount.value, 3_000_000);
  amount.onValueChange(4_000_000);
  const updated = render();
  assert.match(updated, /Còn theo dự toán: 6\.000\.000 đ/);
  assert.match(updated, /không cộng tiền cọc lần nữa/i);
  assert.equal(state.inputs.find(input => /đã chi|đã cọc/i.test(input['aria-label'] || '')).value, 4_000_000);
  assert.ok(state.forms[0]);
  t.mock.method(globalThis, 'setTimeout', () => 0);
  await state.forms[0].onSubmit({ preventDefault() {} });
  assert.equal(state.writes.length, 1);
  assert.equal(state.writes[0].actualCost, 4_000_000);
  assert.equal(state.writes[0].plannedCost, 10_000_000);
  assert.equal(typeof state.writes[0].actualCost, 'number');
  assert.deepEqual(Object.keys(state.writes[0]).sort(), ['actualCost', 'id', 'itemName', 'notes', 'owner', 'plannedCost']);
  assert.equal('contractId' in state.writes[0], false);
  assert.equal('isPaid' in state.writes[0], false);
  assert.equal(state.query.budget.items[0].actualCost, 3_000_000);
  assert.equal(state.deleted.length, 0);
});

test('a new external expense submits the customer-entered deposit once without financial fields', async t => {
  reset([]); render();
  const text = node => typeof node === 'string' ? node : Array.isArray(node) ? node.map(text).join('') : text(node?.props?.children ?? '');
  const add = state.buttons.find(button => /Thêm Khoản Chi/i.test(text(button.children)));
  assert.ok(add); add.onClick(); render();
  const name = state.fields.find(field => field.placeholder?.includes('Thiệp cưới'));
  assert.ok(name); name.onChange({ target: { value: 'Thiệp đặt bên ngoài' } });
  state.inputs.find(input => /dự toán ban đầu/i.test(input['aria-label'] || '')).onValueChange(5_000_000);
  state.inputs.find(input => /đã chi|đã cọc/i.test(input['aria-label'] || '')).onValueChange(1_000_000);
  const form = render();
  assert.match(form, /Còn theo dự toán: 4\.000\.000 đ/);
  t.mock.method(globalThis, 'setTimeout', () => 0);
  await state.forms[0].onSubmit({ preventDefault() {} });
  assert.equal(state.writes.length, 1);
  assert.deepEqual(state.writes[0], { itemName: 'Thiệp đặt bên ngoài', plannedCost: 5_000_000, actualCost: 1_000_000, owner: 'Shared' });
  assert.equal(state.query.budget.items.length, 0);
  assert.equal(state.deleted.length, 0);
});

test('personal expense uses the shared native dialog and Escape closes without writing', () => {
  reset(); render();
  state.buttons.find(button => button.title === 'Sửa khoản chi').onClick();
  const open = render();
  assert.match(open, /<dialog\b/);
  assert.equal(state.dialogs.length, 1);
  let prevented = false;
  state.dialogs[0].onCancel({ preventDefault() { prevented = true; } });
  assert.equal(prevented, true);
  assert.doesNotMatch(render(), /<dialog\b/);
  assert.equal(state.writes.length, 0);
  assert.equal(state.deleted.length, 0);
});

test('failed expense save shows an inline retry error and retains the same cumulative draft', async t => {
  reset(); render();
  state.buttons.find(button => button.title === 'Sửa khoản chi').onClick(); render();
  state.inputs.find(input => /đã chi|đã cọc/i.test(input['aria-label'] || '')).onValueChange(4_000_000);
  state.query.saveItem = async payload => { state.writes.push(structuredClone(payload)); throw new Error('Kết nối tạm thời bị gián đoạn'); };
  render();
  await state.forms[0].onSubmit({ preventDefault() {} });
  const failed = render();
  assert.match(failed, /role="alert"/);
  assert.match(failed, /Kết nối tạm thời bị gián đoạn/);
  assert.equal(state.inputs.find(input => /đã chi|đã cọc/i.test(input['aria-label'] || '')).value, 4_000_000);
  assert.match(failed, /Còn theo dự toán: 6\.000\.000 đ/);
  state.query.saveItem = async payload => { state.writes.push(structuredClone(payload)); };
  render();
  t.mock.method(globalThis, 'setTimeout', () => 0);
  await state.forms[0].onSubmit({ preventDefault() {} });
  assert.equal(state.writes.length, 2);
  assert.deepEqual(state.writes[0], state.writes[1]);
  assert.doesNotMatch(render(), /<dialog\b/);
});

test('pending expense save rejects duplicate submits and keeps the dialog until completion', async t => {
  let resolveSave;
  reset(); render();
  state.buttons.find(button => button.title === 'Sửa khoản chi').onClick(); render();
  state.query.saveItem = async payload => {
    state.writes.push(structuredClone(payload));
    await new Promise(resolve => { resolveSave = resolve; });
  };
  render();
  const submit = state.forms[0].onSubmit;
  t.mock.method(globalThis, 'setTimeout', () => 0);
  const first = submit({ preventDefault() {} });
  await submit({ preventDefault() {} });
  assert.equal(state.writes.length, 1);
  state.query.isSaving = true;
  const busy = render();
  state.dialogs[0].onCancel({ preventDefault() {} });
  assert.match(render(), /<dialog\b/);
  assert.match(busy, /<fieldset\b[^>]*disabled/);
  assert.equal(state.buttons.find(button => button.children === 'Hủy Bỏ').disabled, true);
  resolveSave(); await first;
  state.query.isSaving = false;
  assert.doesNotMatch(render(), /<dialog\b/);
  assert.equal(state.writes.length, 1);
});
