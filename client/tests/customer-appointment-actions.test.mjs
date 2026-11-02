import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

// Exercise the real MyAppointments markup/callbacks with controlled read/mutation
// hooks and the existing modal shell as a leaf. This is not browser/API/SQL QA.
const state = globalThis[Symbol.for('wedding.tests.customer-appointment-actions')] = {};
const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const controlledHooks = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.customer-appointment-actions')];
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
  export function useLayoutEffect(effect) { state.cursor++; effect(); }
`);
const jsx = moduleUrl(`
  import { jsx as originalJsx, jsxs as originalJsxs, Fragment } from ${JSON.stringify(import.meta.resolve('react/jsx-runtime'))};
  const state = globalThis[Symbol.for('wedding.tests.customer-appointment-actions')];
  const wrap = original => (type, props, key) => {
    if (type === 'button') state.buttons.push(props);
    return original(type, props, key);
  };
  export const jsx = wrap(originalJsx), jsxs = wrap(originalJsxs); export { Fragment };
`);
const leaves = moduleUrl(`
  import { createElement } from ${JSON.stringify(import.meta.resolve('react'))};
  const state = globalThis[Symbol.for('wedding.tests.customer-appointment-actions')];
  export const useMyBookings = () => state.query;
  export const useAppointmentAction = () => state.mutation;
  export const useAuthStore = selector => selector({user:state.user});
  export const useNotificationFocus = () => ({focusId:null,missing:false});
  export const downloadAppointmentCalendar = async id => { state.calendar.push(id); };
  export const apiError = error => error?.message || 'QA API unavailable';
  export const vnd = amount => amount + ' đ';
  export const vnDateTime = value => value;
  export const Empty = ({children}) => createElement('p', null, children);
  export const ErrorText = ({children}) => children ? createElement('p', {role:'alert'}, children) : null;
  export const Panel = ({children,action}) => createElement('section', null, action, children);
  export const StatusPill = ({status}) => createElement('span', null, status);
  export const Link = ({to,children,...props}) => createElement('a', {href:to,...props}, children);
  export function ComboDialog(props) { state.dialogs.push(props); return createElement('section', {role:'dialog','aria-label':props.title}, props.children); }
`);
const actualSource = await readFile(new URL('../src/features/journey/appointments/MyAppointments.tsx', import.meta.url), 'utf8');
let output = ts.transpileModule(actualSource, {compilerOptions:{
  target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX,
}}).outputText;
for (const [name,target] of Object.entries({
  react:controlledHooks,'react/jsx-runtime':jsx,'lucide-react':import.meta.resolve('lucide-react'),
  'react-router-dom':leaves,'../api':leaves,'../../../store/authStore':leaves,
  '../bookingApi':leaves,'../ui':leaves,'../format':leaves,
  '../../notifications/useNotificationFocus':leaves,'../combo/ComboDialog':leaves,
})) for (const quote of ['"',"'"]) output = output.replaceAll('from '+quote+name+quote,'from '+JSON.stringify(target));
assert.doesNotMatch(output,/from\s+["']\.\.?\//,'All real component dependencies must be mapped');
const { MyAppointments } = await import(moduleUrl(output));

const appointment = (overrides={}) => ({
  id:'qa-appointment',code:'LH-QA-1',typeName:'Tư vấn dịch vụ',vendorName:'Shop QA',
  startAt:'2026-12-12T02:00:00Z',attendees:2,status:'RescheduleProposed',depositAmount:200_000,
  depositPaid:true,proposedStartAt:'2026-12-13T03:00:00Z',proposedById:'qa-vendor',rescheduleCount:0,
  refundDue:0,freeCancellationHours:24,...overrides,
});
function reset(overrides={}) {
  Object.assign(state,{cursor:0,slots:[],buttons:[],dialogs:[],posts:[],calendar:[],refetches:0,
    user:{id:'qa-customer',role:'Customer'},
    query:{data:[appointment(overrides)],isLoading:false,isError:false,isFetching:false,refetch:()=>{state.refetches++;}},
  });
  state.mutation = {
    isPending:false,isError:false,error:null,queuedPending:false,
    reset(){this.isError=false;this.error=null;},
    mutate(payload,callbacks={}) {
      state.posts.push(structuredClone(payload));this.callbacks=callbacks;this.queuedPending=true;
      // Leave isPending false until render to test the synchronous ref guard.
    },
  };
}
function render() {
  state.cursor=0;state.buttons=[];state.dialogs=[];
  if (state.mutation.queuedPending) state.mutation.isPending=true;
  return renderToStaticMarkup(createElement(MyAppointments));
}
function text(value) {
  if (value==null || typeof value==='boolean') return '';
  if (Array.isArray(value)) return value.map(text).join('');
  if (typeof value==='object') return text(value.props?.children);
  return String(value);
}
function button(label) {
  const result=state.buttons.find(props=>text(props.children)===label);
  assert.ok(result,`Missing button ${label}: ${state.buttons.map(p=>text(p.children)).join(', ')}`);
  return result;
}
function open(label='Từ chối giờ mới & hủy lịch') {
  render();button(label).onClick();const html=render();assert.equal(state.dialogs.length,1);return html;
}
function settle(error=null) {
  state.mutation.queuedPending=false;state.mutation.isPending=false;
  state.mutation.isError=!!error;state.mutation.error=error;
  if (error) state.mutation.callbacks.onError?.(error);
  else state.mutation.callbacks.onSuccess?.();
  state.mutation.callbacks.onSettled?.();
}

test('declining a proposed time previews the exact appointment and its full cancellation outcome, without a request',()=>{
  reset();const html=open();
  assert.equal(state.posts.length,0);
  assert.match(html,/Từ chối giờ mới và hủy lịch hẹn/);
  assert.match(html,/hủy toàn bộ lịch hẹn, không chỉ từ chối giờ mới/);
  assert.match(html,/Giờ hẹn cũ cũng không được giữ lại/);
  assert.match(html,/không tự thực hiện hoàn tiền/);
  for (const detail of ['LH-QA-1','Shop QA','Tư vấn dịch vụ','2026-12-12T02:00:00Z','2026-12-13T03:00:00Z']) assert.ok(html.includes(detail));
  assert.equal(button('Quay lại').autoFocus,true);
  button('Quay lại').onClick();render();assert.equal(state.dialogs.length,0);assert.equal(state.posts.length,0);
});

test('dialog shell close/Escape/backdrop callback cancels confirmation with no mutation',()=>{
  reset();open();state.dialogs[0].onClose();render();assert.equal(state.dialogs.length,0);assert.equal(state.posts.length,0);
});

test('only explicit confirmation submits the existing decline operation, once before React pending state updates',()=>{
  reset();open();const confirm=button('Xác nhận từ chối và hủy lịch');const close=state.dialogs[0].onClose;
  confirm.onClick();confirm.onClick();close();render();
  assert.equal(state.posts.length,1);
  assert.equal(state.posts[0].id,'qa-appointment');assert.equal(state.posts[0].op,'decline-proposal');
  assert.equal(state.posts[0].note,'Không phù hợp giờ đề xuất');
  assert.equal(state.dialogs.length,1);
  for (const label of ['Đồng ý giờ mới','Từ chối giờ mới & hủy lịch','Hủy lịch hẹn','Quay lại','Đang gửi...']) assert.equal(button(label).disabled,true,label);
  settle();render();assert.equal(state.dialogs.length,0);
});

test('normal cancellation also requires exact-appointment confirmation and retains existing API semantics',()=>{
  reset({status:'Confirmed',proposedStartAt:undefined,proposedById:undefined});const html=open('Hủy lịch hẹn');
  assert.match(html,/Bạn có chắc muốn hủy toàn bộ lịch hẹn này/);assert.equal(state.posts.length,0);
  button('Xác nhận hủy lịch').onClick();
  assert.equal(state.posts.length,1);assert.equal(state.posts[0].op,'cancel');assert.equal(state.posts[0].note,'Khách hủy');
});

test('API failure retains the selected appointment and error for a deliberate retry',()=>{
  reset();open();button('Xác nhận từ chối và hủy lịch').onClick();
  settle(new Error('QA request failed'));const html=render();
  assert.equal(state.dialogs.length,1);assert.match(html,/QA request failed/);assert.match(html,/LH-QA-1/);
  assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,false);
  button('Xác nhận từ chối và hủy lịch').onClick();assert.equal(state.posts.length,2);
  assert.deepEqual(state.posts[1],state.posts[0]);settle();render();assert.equal(state.dialogs.length,0);
});

test('a changed or removed appointment cannot be confirmed through a retained stale callback',()=>{
  for (const latest of [[appointment({status:'Cancelled'})],[]]) {
    reset();open();const retained=button('Xác nhận từ chối và hủy lịch').onClick;
    state.query.data=latest;let html=render();
    assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,true);retained();html=render();
    assert.equal(state.posts.length,0);assert.match(html,/Lịch hẹn đã thay đổi/);assert.equal(state.dialogs.length,1);
  }
});

test('changed proposed time after preview cannot be silently rejected as the previously reviewed time',()=>{
  reset();open();const retained=button('Xác nhận từ chối và hủy lịch').onClick;
  state.query.data=[appointment({proposedStartAt:'2026-12-14T03:00:00Z'})];render();retained();
  assert.equal(state.posts.length,0);assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,true);
});

test('read failure or refresh in progress blocks confirmation, preserves preview and provides read-only retry',()=>{
  for (const flags of [{isError:true,isFetching:false},{isError:false,isFetching:true}]) {
    reset();open();const retained=button('Xác nhận từ chối và hủy lịch').onClick;
    Object.assign(state.query,flags);const html=render();retained();
    assert.equal(state.posts.length,0);assert.equal(state.dialogs.length,1);assert.match(html,/Cần tải lại danh sách lịch hẹn thành công/);
    assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,true);
    const retry=button('Tải lại lịch hẹn');assert.equal(retry.disabled,flags.isFetching);
    if (!retry.disabled) {retry.onClick();assert.equal(state.refetches,1);assert.equal(state.posts.length,0);}
    Object.assign(state.query,{isError:false,isFetching:false});render();
    assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,false);
  }
});

test('changing the actor or losing Customer role after preview never redirects the retained action to that actor',()=>{
  for (const user of [{id:'other-customer',role:'Customer'},{id:'qa-customer',role:'VendorOwner'},null]) {
    reset();open();const retained=button('Xác nhận từ chối và hủy lịch').onClick;
    state.user=user;const html=render();retained();
    assert.equal(state.posts.length,0);assert.match(html,/Phiên đăng nhập đã thay đổi/);
    assert.equal(button('Xác nhận từ chối và hủy lịch').disabled,true);
    if (user?.role !== 'Customer') assert.equal(button('Đồng ý giờ mới').disabled,true);
  }
});

for (const scenario of [
  {status:'AwaitingDeposit',label:'Báo đã chuyển cọc',op:'pay-deposit',depositPaid:false},
  {status:'RescheduleProposed',label:'Đồng ý giờ mới',op:'accept-proposal'},
]) test(`${scenario.op}: immediate duplicate/stale clicks are guarded and mutation actions disable while pending`,()=>{
  reset(scenario);render();const retained=button(scenario.label).onClick;
  retained();retained();render();assert.equal(state.posts.length,1);assert.equal(state.posts[0].op,scenario.op);
  assert.equal(button(scenario.label).disabled,true);assert.equal(button('Hủy lịch hẹn').disabled,true);
  assert.equal(button('Thêm vào lịch (.ics)').disabled,undefined);
});

test('a direct mutation callback is rejected after a newer read changes the appointment',()=>{
  reset();render();const retained=button('Đồng ý giờ mới').onClick;
  state.query.data=[appointment({proposedById:'qa-customer'})];render();retained();
  assert.equal(state.posts.length,0);assert.match(render(),/Lịch hẹn đã thay đổi/);
});

test('failed initial reads with cached data expose retry and no mutation actions',()=>{
  reset();state.query.isError=true;const html=render();
  assert.match(html,/Chưa tải được lịch hẹn/);assert.equal(state.dialogs.length,0);
  assert.deepEqual(state.buttons.map(p=>text(p.children)),['Thử lại']);button('Thử lại').onClick();assert.equal(state.refetches,1);
  assert.equal(state.posts.length,0);
});

test('reschedule retains Vietnam-time payload and blocks duplicate prompts while submitting',()=>{
  reset({status:'Confirmed'});let prompts=0;const previousPrompt=globalThis.prompt;
  globalThis.prompt=()=>{prompts++;return '2026-12-14T10:00';};
  try {
    render();const retained=button('Đề xuất đổi lịch').onClick;retained();retained();
    assert.equal(prompts,1);assert.equal(state.posts.length,1);assert.equal(state.posts[0].op,'reschedule');
    assert.equal(state.posts[0].newStartAt,'2026-12-14T03:00:00.000Z');
    render();assert.equal(button('Đề xuất đổi lịch').disabled,true);
  } finally { if (previousPrompt===undefined) delete globalThis.prompt;else globalThis.prompt=previousPrompt; }
});
