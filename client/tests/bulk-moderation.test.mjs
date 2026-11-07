import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';
import { moderationActor, sameModerationActor, startModerationPolling } from '../src/features/admin/bulkModerationModel.ts';

const session = (patch={}) => ({ user:{id:'staff-a',role:'SuperAdmin'},token:'synthetic-a',sessionRevision:1,validated:true,...patch });
const result = (patch={}) => ({ approvedListings:1,approvedServices:0,approvedCombos:0,totalApproved:1,attempted:1,skipped:0,hasMore:false,...patch });
const settle = () => new Promise(resolve => setImmediate(resolve));
function clock() {
  let key=0; const timers=new Map();
  return { timers, schedule:(fn,delay)=>{const id=++key;timers.set(id,{fn,delay});return id;},cancel:id=>timers.delete(id),
    fire(){const [id,timer]=timers.entries().next().value;timers.delete(id);timer.fn();return timer.delay;} };
}
function polling(extra={}) {
  const c=clock(),calls={approve:0,results:[],errors:[]};
  const stop=startModerationPolling({isCurrent:()=>true,hasPending:()=>true,approve:async()=>{calls.approve++;return result();},
    onResult:r=>calls.results.push(r),onError:e=>calls.errors.push(e),schedule:c.schedule,cancel:c.cancel,...extra});
  return {c,calls,stop};
}

test('bulk authorization snapshots require validated active staff session, exact actor/token/revision',()=>{
  const actor=moderationActor(session()); assert.ok(actor);assert.equal(sameModerationActor(actor,session()),true);
  for(const change of [{validated:false},{token:null},{sessionRevision:2},{token:'synthetic-b'},{user:{id:'staff-b',role:'SuperAdmin'}},{user:{id:'staff-a',role:'Moderator'}},{user:{id:'staff-a',role:'VendorOwner'}}])
    assert.equal(sameModerationActor(actor,session(change)),false);
  assert.equal(moderationActor(session({user:{id:'customer',role:'Customer'}})),null);
});

test('automatic mode starts after confirmation delay and waits at least3 seconds between settled batches',async()=>{
  const p=polling();assert.equal(p.calls.approve,0);assert.equal(p.c.fire(),3000);await settle();
  assert.equal(p.calls.approve,1);assert.equal(p.calls.results.length,1);assert.equal(p.c.timers.size,1);
  assert.equal(p.c.fire(),3000);await settle();assert.equal(p.calls.approve,2);p.stop();assert.equal(p.c.timers.size,0);
});

test('slow batch cannot overlap itself or immediately retry when status changes',async()=>{
  let release;let calls=0;const p=polling({approve:()=>{calls++;return new Promise(resolve=>{release=resolve;});}});
  p.c.fire();assert.equal(calls,1);assert.equal(p.c.timers.size,0);await settle();assert.equal(calls,1);
  release(result());await settle();assert.equal(p.c.timers.size,1);assert.equal(p.c.fire(),3000);assert.equal(calls,2);p.stop();release(result());
});

test('skipped invalid content stops polling instead of spinning on pendingCount',async()=>{
  const p=polling({approve:async()=>result({totalApproved:0,skipped:1})});p.c.fire();await settle();
  assert.equal(p.calls.results.length,1);assert.equal(p.c.timers.size,0);
});

test('infrastructure error stops polling and is reported once',async()=>{
  const p=polling({approve:async()=>{throw new Error('Synthetic outage');}});p.c.fire();await settle();
  assert.equal(p.calls.errors.length,1);assert.equal(p.c.timers.size,0);
});

test('logout, actor switch and unmount prevent subsequent requests and late result callbacks',async()=>{
  let current=false;const p=polling({isCurrent:()=>current});p.c.fire();await settle();assert.equal(p.calls.approve,0);assert.equal(p.c.timers.size,0);
  current=true;let release;const late=polling({isCurrent:()=>current,approve:()=>new Promise(resolve=>{release=resolve;})});late.c.fire();current=false;release(result());await settle();
  assert.equal(late.calls.results.length,0);assert.equal(late.c.timers.size,0);
  const unmounted=polling();unmounted.stop();assert.equal(unmounted.c.timers.size,0);assert.equal(unmounted.calls.approve,0);
});

const state=globalThis[Symbol.for('wedding.tests.bulk-moderation')]={};
const moduleUrl=source=>`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`;
async function actual(path,aliases={}){
  let output=ts.transpileModule(await readFile(new URL(path,import.meta.url),'utf8'),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX}}).outputText;
  for(const [name,target] of Object.entries({'react/jsx-runtime':import.meta.resolve('react/jsx-runtime'),...aliases}))
    output=output.replaceAll(`from "${name}"`,`from ${JSON.stringify(target)}`);
  assert.doesNotMatch(output,/from\s+["']\.\.?\//,'All real dependencies mapped');return import(moduleUrl(output));
}
const auth=moduleUrl(`const state=globalThis[Symbol.for('wedding.tests.bulk-moderation')];export const useAuthStore=Object.assign(()=>state.session,{getState:()=>state.session});`);
const hooks=moduleUrl(`const state=globalThis[Symbol.for('wedding.tests.bulk-moderation')];export const useRef=value=>({current:value});export const useEffect=fn=>{state.cleanups.push(fn());};`);
const query=moduleUrl(`const state=globalThis[Symbol.for('wedding.tests.bulk-moderation')];export const useMutation=config=>config;export const useQuery=config=>config;export const useQueryClient=()=>({invalidateQueries:async()=>{state.invalidations++;}});`);
const axios=moduleUrl(`const state=globalThis[Symbol.for('wedding.tests.bulk-moderation')];export default {post:async(url,body,config)=>{state.posts.push({url,body,config});return state.respond();}};`);
const {useAutoApproveAll: invokeBulkHook}=await actual('../src/features/admin/useModerationQueue.ts',{
  react:hooks,'@tanstack/react-query':query,'../../store/authStore':auth,'../../services/axiosClient':axios,
  './bulkModerationModel':new URL('../src/features/admin/bulkModerationModel.ts',import.meta.url).href,
});
function setupHook(){Object.assign(state,{session:session(),posts:[],cleanups:[],invalidations:0,respond:async()=>({data:result()})});return invokeBulkHook();}

test('real bulk hook pins authorization and bounds batch, rejects stale actor before network',async()=>{
  const hook=setupHook(),actor=moderationActor(state.session),request={type:'all',actor};
  state.session=session({token:'changed'});await assert.rejects(hook.mutationFn(request),/Phiên/);assert.equal(state.posts.length,0);
  state.session=session();const response=await hook.mutationFn(request);assert.equal(response.totalApproved,1);
  assert.equal(state.posts[0].config.headers.Authorization,'Bearer synthetic-a');assert.equal(state.posts[0].config.params.batchSize,20);assert.ok(state.posts[0].config.signal);assert.equal(hook.retry,false);
});

test('real bulk hook prevents concurrent flight and aborts request on actor cleanup',async()=>{
  const hook=setupHook(),request={type:'listing',actor:moderationActor(state.session)};let release;
  state.respond=()=>new Promise(resolve=>{release=resolve;});const pending=hook.mutationFn(request);
  await assert.rejects(hook.mutationFn(request),/Đang xử lý/);assert.equal(state.posts.length,1);
  state.cleanups.forEach(cleanup=>cleanup());assert.equal(state.posts[0].config.signal.aborted,true);
  release({data:result()});await assert.rejects(pending,/Phiên/);
  await hook.onSuccess(result(),request);assert.equal(state.invalidations,0);
});

const shell=moduleUrl(`import {createElement} from ${JSON.stringify(import.meta.resolve('react'))};export const ComboDialog=({title,children})=>createElement('div',{'aria-label':title},children);`);
const {BulkModerationConfirmationDialog}=await actual('../src/features/admin/BulkModerationConfirmationDialog.tsx',{'../journey/combo/ComboDialog':shell});
function elements(node,type){if(!node||typeof node!=='object')return[];if(Array.isArray(node))return node.flatMap(child=>elements(child,type));return[...(node.type===type?[node]:[]),...elements(node.props?.children,type)];}
test('bulk dialog explicitly warns scope/unverified vendors/session limit; render and cancel never approve',()=>{
  const calls={confirm:0,cancel:0};const props={proposal:{mode:'automatic',type:'all',actor:moderationActor(session())},busy:false,onConfirm:()=>calls.confirm++,onCancel:()=>calls.cancel++};
  const html=renderToStaticMarkup(createElement(BulkModerationConfirmationDialog,props));
  for(const text of ['chưa được xác minh','toàn hệ thống','20 mục','3 giây','Đóng màn','Không xác minh shop'])assert.ok(html.includes(text),text);
  assert.deepEqual(calls,{confirm:0,cancel:0});const tree=BulkModerationConfirmationDialog(props);const buttons=elements(tree,'button');
  buttons[0].props.onClick();assert.deepEqual(calls,{confirm:0,cancel:1});buttons[1].props.onClick();assert.equal(calls.confirm,1);
  const busy=BulkModerationConfirmationDialog({...props,busy:true});for(const button of elements(busy,'button')){assert.equal(button.props.disabled,true);button.props.onClick();}assert.equal(calls.confirm,1);
});
