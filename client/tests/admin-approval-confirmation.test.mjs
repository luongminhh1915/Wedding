import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

// Actual caller callbacks and payload builders, controlled hooks/leaf UI only.
// This is not browser lifecycle, API authorization, persistence or SQL evidence.
const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')] = {};
const moduleUrl = source => `data:text/javascript;base64,${Buffer.from(source).toString('base64')}`;
const hooks = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')];
  export function useState(initial) {
    const index = state.cursor++;
    if (!(index in state.slots)) state.slots[index] = typeof initial === 'function' ? initial() : initial;
    return [state.slots[index], next => { state.slots[index] = typeof next === 'function' ? next(state.slots[index]) : next; }];
  }
  export function useRef(initial) {
    const index = state.cursor++;
    if (!(index in state.slots)) state.slots[index] = { current: initial };
    return state.slots[index];
  }
`);
const jsx = moduleUrl(`
  import { jsx as originalJsx, jsxs as originalJsxs, Fragment } from ${JSON.stringify(import.meta.resolve('react/jsx-runtime'))};
  const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')];
  const wrap = original => (type, props, key) => {
    if (type === 'button') state.buttons.push(props);
    if (type === 'textarea') state.fields.push(props);
    return original(type, props, key);
  };
  export const jsx = wrap(originalJsx), jsxs = wrap(originalJsxs); export { Fragment };
`);
const leaves = moduleUrl(`
  import { createElement } from ${JSON.stringify(import.meta.resolve('react'))};
  const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')];
  export const apiError = error => error?.message || 'QA API error';
  export function Link({to,children,...props}) { return createElement('a', {href:to,...props}, children); }
  export const useSearchParams = () => [new URLSearchParams(), () => {}];
  export const useAuthStore = selector => selector ? selector({ user: {id:'qa-admin',role:'SuperAdmin'} }) : { user: {id:'qa-admin',role:'SuperAdmin'},token:'synthetic-test-token' };
  export const Empty = ({children}) => createElement('div', null, children);
  export const ErrorText = ({children}) => children ? createElement('p', {role:'alert'}, children) : null;
  export const Panel = ({children}) => createElement('section', null, children);
  export const StatusPill = ({status,label}) => createElement('span', null, label || status);
  export const Pagination = () => null, ServiceDetails = () => null, ComboPhoto = () => null, ComboTemplateEditor = () => null, ComboLifecycleCard = () => null;
  export const listingImagePreview = url => url;
  export const vnd = value => String(value), comboVnd = value => String(value);
  export const useModerationQueue = () => ({data:{items:[],pendingCount:0}});
  export function ApprovalConfirmationDialog(props) { state.dialogs.push(props); return createElement('div', {role:'dialog'}, props.details.title); }
  export const useCatalog = () => ({data:[]}), useComboTemplates = () => ({data:[]}), useRules = () => ({data:[]});
`);
const mutationHooks = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')];
  export { useCatalog, useComboTemplates, useRules } from ${JSON.stringify(leaves)};
  export const useMutation = config => state.getMutation('queue', config);
  export const useComboMutation = path => state.getMutation(path);
  export const useQuery = () => ({});
  export const useQueryClient = () => ({invalidateQueries:async () => {state.invalidations++;}});
`);
const axios = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.admin-approval-confirmation')];
  export default {post:async (url,body,config) => {state.posts.push({url,body:structuredClone(body),params:structuredClone(config?.params)}); return {data:{}};}};
`);

async function actualModule(path, aliases = {}) {
  const source = await readFile(new URL(path, import.meta.url), 'utf8');
  let output = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  for (const [name, target] of Object.entries({
    react: hooks, 'react/jsx-runtime': jsx, 'lucide-react': import.meta.resolve('lucide-react'),
    'react-router-dom': leaves, '@tanstack/react-query': mutationHooks, ...aliases,
  })) for (const quote of ['"', "'"]) output = output.replaceAll(`from ${quote}${name}${quote}`, `from ${JSON.stringify(target)}`);
  assert.doesNotMatch(output, /from\s+["']\.\.?\//, `Unmapped runtime dependency in ${path}`);
  return import(moduleUrl(output));
}
const model = new URL('../src/features/admin/moderationQueueModel.ts', import.meta.url).href;
const comboModel = new URL('../src/features/journey/combo/comboTemplateEditorModel.ts', import.meta.url).href;
const { ReviewDetail } = await actualModule('../src/features/admin/ModerationQueue.tsx', {
  '../../store/authStore': leaves, '../../services/axiosClient': axios,
  '../../components/Pagination': leaves, '../customer/components/ServiceDetails': leaves,
  '../vendor-portal/components/listingEditModel': leaves, '../journey/api': leaves,
  '../journey/combo/comboTemplateEditorModel': comboModel, './useModerationQueue': leaves,
  './moderationQueueModel': model, './ApprovalConfirmationDialog': leaves,
});
const { ReviewItem } = await actualModule('../src/features/journey/admin/ComboAdministration.tsx', {
  '../api': leaves, '../comboApi': mutationHooks, '../../../store/authStore': leaves,
  '../ui': leaves, '../format': leaves, '../combo/ComboLifecycleCard': leaves,
  '../combo/ComboTemplateEditor': leaves, './serviceItemReview': new URL('../src/features/journey/admin/serviceItemReview.ts', import.meta.url).href,
  '../../admin/ApprovalConfirmationDialog': leaves,
});
const { ComboLifecycleCard } = await actualModule('../src/features/journey/combo/ComboLifecycleCard.tsx', {
  '../api': leaves, '../comboApi': mutationHooks, './comboTemplateEditorModel': comboModel,
  './ComboPhoto': leaves, './FixedComboServicePicker': leaves, '../../admin/ApprovalConfirmationDialog': leaves,
});

const id = '11111111-1111-4111-8111-111111111111';
const revisionId = '22222222-2222-4222-8222-222222222222';
const otherId = '33333333-3333-4333-8333-333333333333';
const version = 'a'.repeat(64);
const item = () => ({ id, listingId: otherId, moderationStatus:'Pending', reviewVersion:version,
  name:'Dịch vụ QA', vendorName:'Shop QA', basePrice:1_000_000, unit:'Package', regions:'Hà Nội',
  minGuests:1,maxGuests:100,cancellationPolicy:'Điều kiện QA',variants:[],addOns:[],isActive:false });
const combo = () => ({ id, name:'Combo công bố QA', ownerVendorId:otherId, ownerVendorName:'Shop QA',
  style:'Hiện đại', moderationStatus:'Approved',isActive:false,items:[],revisionHistory:[],
  latestRevision:{id:revisionId,sequence:4,status:'Pending',createdAt:'2026-10-21T00:00:00Z',
    proposal:{name:'Combo đề xuất QA',items:[],pricingMode:'Dynamic'}} });
const listing = () => ({id,type:'listing',listing:{id,title:'Bài dịch vụ QA',vendorBrandName:'Shop QA',
  categoryName:'Media',location:'Hà Nội',minPrice:1_000_000,maxPrice:2_000_000,description:'Thông tin QA',media:[],
  latestRevision:{id:revisionId,sequence:7,status:'Pending'}}});
const scenarios = [
  { name:'queue listing', component:ReviewDetail, make:()=>({detail:listing()}),
    title:'Bài dịch vụ QA', version:'Phiên bản #7', approve:'Phê duyệt phiên bản', reject:'Từ chối kèm lý do',
    expected: () => ({url:`/api/listings/${id}/approve`,body:null,params:{revisionId}}),
    stale: props => {props.detail.listing.latestRevision.id=otherId;},
    nonpending: props => {props.detail.listing.latestRevision.status='Approved';} },
  { name:'queue service', component:ReviewDetail, make:()=>({detail:{id,type:'service-item',serviceItem:item()}}),
    title:'Dịch vụ QA', version:'Nội dung hiện tại đang xem', approve:'Phê duyệt phiên bản', reject:'Từ chối kèm lý do',
    expected: note => ({url:`/api/combos/items/${id}/review`,body:{approve:true,note,reviewVersion:version},params:undefined}),
    stale: props => {props.detail.serviceItem.reviewVersion='b'.repeat(64);},
    nonpending: props => {props.detail.serviceItem.moderationStatus='Approved';} },
  { name:'queue combo', component:ReviewDetail, make:()=>({detail:{id,type:'combo',combo:combo()}}),
    title:'Combo đề xuất QA', version:'Phiên bản #4', approve:'Phê duyệt phiên bản', reject:'Từ chối kèm lý do',
    expected: note => ({url:`/api/combos/templates/${id}/review`,body:{approve:true,note,revisionId},params:undefined}),
    stale: props => {props.detail.combo.latestRevision.id=otherId;},
    nonpending: props => {props.detail.combo.latestRevision.status='Superseded';} },
  { name:'combo administration service', component:ReviewItem, make:()=>({item:item()}),
    title:'Dịch vụ QA', version:'Nội dung hiện tại đang xem', approve:'Duyệt dịch vụ', reject:'Từ chối dịch vụ',
    expected: note => ({url:`/api/combos/items/${id}/review`,body:{approve:true,note,reviewVersion:version},params:undefined}),
    stale: props => {props.item.reviewVersion='b'.repeat(64);},
    nonpending: props => {props.item.moderationStatus='Approved';} },
  { name:'combo lifecycle card', component:ComboLifecycleCard, make:()=>({template:combo(),staff:true}),
    title:'Combo đề xuất QA', version:'phiên bản #4', approve:'Phê duyệt phiên bản #4', reject:'Từ chối kèm lý do',
    expected: note => ({url:`/api/combos/templates/${id}/review`,body:{approve:true,note,revisionId},params:undefined}),
    stale: props => {props.template.latestRevision.id=otherId;},
    nonpending: props => {props.template.latestRevision.status='Superseded';} },
];

function reset() {
  Object.assign(state,{cursor:0,slots:[],buttons:[],fields:[],dialogs:[],posts:[],calls:[],invalidations:0,done:0,refreshes:0,mutations:new Map()});
  state.getMutation = (path, config) => {
    if (!state.mutations.has(path)) {
      const mutation = {isPending:false,isError:false,error:null,queuedPending:false,
        reset(){this.isError=false;this.error=null;},
        mutate(payload, callbacks = {}) {
          state.calls.push({path,payload:structuredClone(payload)});
          this.callbacks=callbacks;this.queuedPending=true;
          // Deliberately leave isPending false until the next render. The stale
          // callback test must pass because of the caller ref, not this adapter.
          this.task = this.config ? Promise.resolve(this.config.mutationFn(payload)) : Promise.resolve(state.posts.push({url:path,body:structuredClone(payload),params:undefined}));
        },
      };
      state.mutations.set(path,mutation);
    }
    const mutation=state.mutations.get(path);mutation.config=config;return mutation;
  };
}
function render(scenario, props) {
  state.cursor=0;state.buttons=[];state.fields=[];state.dialogs=[];
  for (const mutation of state.mutations.values()) if (mutation.queuedPending) mutation.isPending=true;
  return renderToStaticMarkup(createElement(scenario.component,{
    onRefresh:()=>{state.refreshes++;},onDone:()=>{state.done++;},onEdit:()=>{},...props,
  }));
}
function text(value) {
  if (value == null || typeof value === 'boolean') return '';
  if (Array.isArray(value)) return value.map(text).join('');
  if (typeof value === 'object') return text(value.props?.children);
  return String(value);
}
function button(label) {
  const found=state.buttons.find(props=>text(props.children)===label);
  assert.ok(found,`Missing action ${label}; found ${state.buttons.map(p=>text(p.children)).join(', ')}`);return found;
}
function note(value) {assert.equal(state.fields.length,1);state.fields[0].onChange({target:{value}});}
function dialog() {assert.equal(state.dialogs.length,1);return state.dialogs[0];}
async function settle(error) {
  const last=state.calls.at(-1);assert.ok(last);
  const mutation=state.mutations.get(last.path);await mutation.task;
  mutation.isPending=false;mutation.queuedPending=false;mutation.isError=!!error;mutation.error=error || null;
  if (error) {await mutation.config?.onError?.(error);await mutation.callbacks?.onError?.(error);}
  else {await mutation.config?.onSuccess?.();await mutation.callbacks?.onSuccess?.();}
  await mutation.config?.onSettled?.();await mutation.callbacks?.onSettled?.();
}
function open(scenario, props, message='  Ghi chú QA  ') {
  render(scenario,props);note(message);render(scenario,props);button(scenario.approve).onClick();render(scenario,props);return dialog();
}

for (const scenario of scenarios) {
  test(`${scenario.name}: first approve previews exact target, cancel preserves note and makes no request`,()=>{
    reset();const props=scenario.make();const approval=open(scenario,props);
    assert.equal(state.calls.length,0);assert.equal(state.posts.length,0);
    assert.equal(approval.details.title,scenario.title);assert.equal(approval.details.vendorName,'Shop QA');assert.equal(approval.details.versionLabel,scenario.version);
    approval.onCancel();render(scenario,props);assert.equal(state.dialogs.length,0);
    assert.equal(state.fields[0].value,'  Ghi chú QA  ');assert.equal(state.posts.length,0);
  });

  test(`${scenario.name}: confirmation posts exact allowlisted version once and closes only after success`,async()=>{
    reset();const props=scenario.make();const approval=open(scenario,props);
    approval.onConfirm();approval.onConfirm();assert.equal(state.calls.length,1,'synchronous double-click is locked');
    assert.deepEqual(state.posts,[scenario.expected('Ghi chú QA')]);
    render(scenario,props);assert.equal(dialog().busy,true);dialog().onCancel();render(scenario,props);assert.equal(state.dialogs.length,1,'pending action cannot be cancelled');
    await settle();render(scenario,props);assert.equal(state.dialogs.length,0);assert.equal(state.posts.length,1);
    if (scenario.component===ReviewDetail) {assert.equal(state.done,1);assert.equal(state.invalidations,1);}
  });

  test(`${scenario.name}: API failure leaves confirmation and input for a deliberate retry`,async()=>{
    reset();const props=scenario.make();open(scenario,props).onConfirm();
    await settle(new Error('QA API unavailable'));render(scenario,props);
    assert.equal(dialog().error,'QA API unavailable');assert.equal(dialog().busy,false);assert.equal(state.fields[0].value,'  Ghi chú QA  ');
    dialog().onConfirm();assert.equal(state.calls.length,2);assert.deepEqual(state.posts,[scenario.expected('Ghi chú QA'),scenario.expected('Ghi chú QA')]);
    await settle();render(scenario,props);assert.equal(state.dialogs.length,0);
  });

  test(`${scenario.name}: changed version after preview fails closed rather than approving refreshed content`,()=>{
    reset();const props=scenario.make();open(scenario,props);scenario.stale(props);render(scenario,props);dialog().onConfirm();
    const html=render(scenario,props);assert.equal(state.posts.length,0);assert.equal(state.calls.length,0);assert.equal(state.dialogs.length,0);
    assert.match(html,/thay đổi|xem lại|phiên bản/i);
  });

  test(`${scenario.name}: no-longer-pending content after preview cannot be approved`,()=>{
    reset();const props=scenario.make();open(scenario,props);scenario.nonpending(props);render(scenario,props);dialog().onConfirm();
    const html=render(scenario,props);assert.equal(state.posts.length,0);assert.equal(state.dialogs.length,0);assert.match(html,/không còn|tải lại/i);
  });

  test(`${scenario.name}: rejection keeps existing required-reason and direct-decision behavior`,async()=>{
    reset();const props=scenario.make();render(scenario,props);button(scenario.reject).onClick();render(scenario,props);
    assert.equal(state.posts.length,0);assert.equal(state.dialogs.length,0);
    note('  Cần bổ sung QA  ');render(scenario,props);button(scenario.reject).onClick();assert.equal(state.posts.length,1);assert.equal(state.dialogs.length,0);
    const posted=state.posts[0];assert.match(posted.url,/review$|reject$/);assert.equal(posted.body.approve ?? false,false);
    assert.equal(posted.body.note ?? posted.body.reason,'Cần bổ sung QA');await settle();
  });
}

test('combo card never exposes approval to a Vendor/non-staff caller, even with pending content',()=>{
  reset();const props={template:combo(),staff:false};const scenario=scenarios.at(-1);render(scenario,props);
  assert.ok(!state.buttons.some(p=>/^Phê duyệt|^Từ chối/.test(text(p.children))));assert.equal(state.dialogs.length,0);assert.equal(state.posts.length,0);
});

test('combo card loses approval capability when staff flag changes while confirmation is open',()=>{
  reset();const scenario=scenarios.at(-1);const props=scenario.make();open(scenario,props);props.staff=false;render(scenario,props);
  assert.equal(state.dialogs.length,0);assert.ok(!state.buttons.some(p=>/^Phê duyệt/.test(text(p.children))));assert.equal(state.posts.length,0);
});

for (const scenario of scenarios.filter(value=>value.name!=='queue listing')) {
  test(`${scenario.name}: note changed behind confirmation must be reviewed again`,()=>{
    reset();const props=scenario.make();open(scenario,props);note('Nội dung khác');render(scenario,props);dialog().onConfirm();
    render(scenario,props);assert.equal(state.dialogs.length,0);assert.equal(state.posts.length,0);
  });
}

test('legacy combo with no revision rejects same-ID changed content instead of inventing a version',()=>{
  reset();const scenario=scenarios.at(-1);const props=scenario.make();
  delete props.template.latestRevision;props.template.moderationStatus='Pending';
  render(scenario,props);note('Ghi chú legacy');render(scenario,props);
  const approve=state.buttons.find(p=>/^Phê duyệt /.test(text(p.children)));assert.ok(approve);approve.onClick();render(scenario,props);
  assert.equal(dialog().details.title,'Combo công bố QA');assert.match(dialog().details.versionLabel,/bản cũ/);
  props.template.name='Nội dung legacy đã thay đổi';render(scenario,props);dialog().onConfirm();render(scenario,props);
  assert.equal(state.posts.length,0);assert.equal(state.dialogs.length,0);
});
