import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

// Real component parser/selection/submission callbacks with controlled hook
// lifecycle and mutations. Not browser file-picker/API/SQL acceptance evidence.
const state = globalThis[Symbol.for('wedding.tests.guest-import-lifecycle')] = {};
const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const hooks = moduleUrl(`
  const state = globalThis[Symbol.for('wedding.tests.guest-import-lifecycle')];
  export function useState(initial) {
    const index=state.cursor++;
    if (!(index in state.slots)) state.slots[index]=typeof initial==='function'?initial():initial;
    return [state.slots[index],next=>{state.setCalls++;state.slots[index]=typeof next==='function'?next(state.slots[index]):next;}];
  }
  export function useRef(initial) {
    const index=state.cursor++;
    if (!(index in state.slots)) state.slots[index]={current:initial};
    return state.slots[index];
  }
  export function useEffect(effect) {
    const index=state.cursor++;
    if (!(index in state.slots)) {state.slots[index]=true;state.cleanups.push(effect());}
  }
  export function useLayoutEffect(effect) {state.cursor++;effect();}
`);
const jsx = moduleUrl(`
  import { jsx as originalJsx, jsxs as originalJsxs, Fragment } from ${JSON.stringify(import.meta.resolve('react/jsx-runtime'))};
  const state=globalThis[Symbol.for('wedding.tests.guest-import-lifecycle')];
  const wrap=original=>(type,props,key)=>{
    if(type==='button')state.buttons.push(props);
    if(type==='input')state.inputs.push(props);
    return original(type,props,key);
  };
  export const jsx=wrap(originalJsx),jsxs=wrap(originalJsxs);export {Fragment};
`);
const apiSource = await readFile(new URL('../src/features/journey/api.ts',import.meta.url),'utf8');
const apiSyntax = ts.createSourceFile('api.ts',apiSource,ts.ScriptTarget.Latest,true,ts.ScriptKind.TS);
const errorHelper = apiSyntax.statements.find(node=>ts.isFunctionDeclaration(node)&&node.name?.text==='apiError');
assert.ok(errorHelper,'Use the real shared API error helper, not a more permissive mock');
const realApiError = moduleUrl(ts.transpileModule(errorHelper.getText(apiSyntax),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext}}).outputText);
const leaves = moduleUrl(`
  const state=globalThis[Symbol.for('wedding.tests.guest-import-lifecycle')];
  export const useGuests=()=>state.query;
  export const usePlannerAction=()=>state.mutation;
  export {apiError} from ${JSON.stringify(realApiError)};
`);
const source = await readFile(new URL('../src/features/journey/planner/GuestsPlanner.tsx',import.meta.url),'utf8');
let output=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX}}).outputText;
for(const[name,target]of Object.entries({react:hooks,'react/jsx-runtime':jsx,'lucide-react':import.meta.resolve('lucide-react'),'../api':leaves,'./plannerApi':leaves}))
  for(const quote of ['"',"'"])output=output.replaceAll('from '+quote+name+quote,'from '+JSON.stringify(target));
assert.doesNotMatch(output,/from\s+["']\.\.?\//,'All real component dependencies must be mapped');
const{GuestsPlanner}=await import(moduleUrl(output));

function reset(){
  Object.assign(state,{cursor:0,slots:[],buttons:[],inputs:[],posts:[],cleanups:[],setCalls:0,refetches:0,
    query:{data:{myRole:'Owner',guests:[],tables:[]},isLoading:false,isError:false,isFetching:false,refetch:()=>{state.refetches++;}},
  });
  state.mutation={
    isPending:false,isError:false,error:null,queuedPending:false,
    reset(){this.isError=false;this.error=null;},
    mutate(payload,callbacks={}){state.posts.push(structuredClone(payload));this.callbacks=callbacks;this.queuedPending=true;},
    mutateAsync:async()=>{},
  };
}
function render(){
  state.cursor=0;state.buttons=[];state.inputs=[];
  if(state.mutation.queuedPending)state.mutation.isPending=true;
  return renderToStaticMarkup(createElement(GuestsPlanner));
}
function text(value){
  if(value==null||typeof value==='boolean')return'';
  if(Array.isArray(value))return value.map(text).join('');
  return typeof value==='object'?text(value.props?.children):String(value);
}
function button(label){const found=state.buttons.find(props=>text(props.children)===label);assert.ok(found,`Missing ${label}: ${state.buttons.map(p=>text(p.children)).join(', ')}`);return found;}
function hasButton(label){return state.buttons.some(props=>text(props.children)===label);}
function fileInput(){const found=state.inputs.find(props=>props.type==='file');assert.ok(found);return found;}
function choose(file){const target={files:file?[file]:[],value:file?.name||''};fileInput().onChange({target});assert.equal(target.value,'','Same-file retry must remain possible');}
const flush=()=>new Promise(resolve=>setImmediate(resolve));
function deferred(){let resolve,reject;const promise=new Promise((yes,no)=>{resolve=yes;reject=no;});return{promise,resolve,reject};}
function csvFile(name,content,options={}){return{name,size:options.size??Buffer.byteLength(content||''),text:options.text||(()=>Promise.resolve(content))};}
const simple=name=>`name,phone,group,side,partySize,status,dietary,notes\r\n${name},0900000000,Bạn bè,Shared,2,Attending,,`;
async function preview(file=csvFile('guests.csv',simple('Nhóm mới'))){render();choose(file);await flush();const html=render();assert.ok(hasButton('Nhập vào danh sách'));return html;}
function settle(error=null){
  state.mutation.queuedPending=false;state.mutation.isPending=false;state.mutation.isError=!!error;state.mutation.error=error;
  if(error)state.mutation.callbacks.onError?.(error);else state.mutation.callbacks.onSuccess?.();
  state.mutation.callbacks.onSettled?.();
}

test('replacing valid A with invalid B immediately revokes A, including a retained pre-render submit callback',async()=>{
  reset();await preview(csvFile('A.csv',simple('Nhóm A')));const oldSubmit=button('Nhập vào danh sách').onClick;
  choose(csvFile('B.csv','wrong-header\nNhóm B'));oldSubmit();
  assert.equal(state.posts.length,0);let html=render();assert.ok(!hasButton('Nhập vào danh sách'));assert.match(html,/Đang đọc B.csv/);
  await flush();html=render();assert.ok(!hasButton('Nhập vào danh sách'));assert.match(html,/Dùng CSV có cột/);
  assert.doesNotMatch(html,/Kiểm tra tên: Nhóm A/);assert.equal(state.posts.length,0);
});

test('latest selected file wins when an earlier file read completes later',async()=>{
  reset();render();const old=deferred();choose(csvFile('slow-A.csv','',{text:()=>old.promise}));render();
  choose(csvFile('fast-B.csv',simple('Nhóm B')));await flush();let html=render();assert.match(html,/fast-B.csv/);assert.match(html,/Nhóm B/);
  old.resolve(simple('Nhóm A'));await flush();html=render();assert.match(html,/fast-B.csv/);assert.doesNotMatch(html,/Nhóm A/);
  button('Nhập vào danh sách').onClick();assert.equal(state.posts.length,1);assert.equal(state.posts[0].data.guests[0].name,'Nhóm B');
});

test('a stale rejected read cannot replace or add errors to a newer successful preview',async()=>{
  reset();render();const old=deferred();choose(csvFile('A.csv','',{text:()=>old.promise}));render();
  choose(csvFile('B.csv',simple('Nhóm B')));await flush();render();
  old.reject(new Error('OLD FILE ERROR'));await flush();const html=render();
  assert.match(html,/Nhóm B/);assert.doesNotMatch(html,/OLD FILE ERROR/);assert.ok(hasButton('Nhập vào danh sách'));
});

test('cancelling an unfinished read prevents late completion from reopening a preview',async()=>{
  reset();render();const read=deferred();choose(csvFile('slow.csv','',{text:()=>read.promise}));render();
  button('Hủy đọc CSV').onClick();render();read.resolve(simple('Nhóm bị hủy'));await flush();const html=render();
  assert.ok(!hasButton('Nhập vào danh sách'));assert.doesNotMatch(html,/Nhóm bị hủy/);assert.equal(state.posts.length,0);
});

test('cancelled preview cannot submit through an earlier retained callback',async()=>{
  reset();await preview();const submit=button('Nhập vào danh sách').onClick;
  button('Hủy nhập CSV').onClick();submit();render();assert.equal(state.posts.length,0);assert.ok(!hasButton('Nhập vào danh sách'));
});

test('unmounted async reads do not update preview, error or reading state',async()=>{
  reset();render();const read=deferred();choose(csvFile('slow.csv','',{text:()=>read.promise}));render();
  for(const cleanup of state.cleanups)cleanup?.();const before=state.setCalls;
  read.resolve(simple('Unmounted'));await flush();assert.equal(state.setCalls,before);assert.equal(state.posts.length,0);
});

test('same filename can be selected again after read failure and clears the stale error',async()=>{
  reset();render();choose(csvFile('same.csv','',{text:()=>Promise.reject(new Error('FILE READ FAILED'))}));await flush();
  assert.match(render(),/FILE READ FAILED/);choose(csvFile('same.csv',simple('Nhóm thành công')));await flush();const html=render();
  assert.doesNotMatch(html,/FILE READ FAILED/);assert.match(html,/Nhóm thành công/);assert.ok(hasButton('Nhập vào danh sách'));
});

test('oversized replacement clears previous preview without reading file content',async()=>{
  reset();await preview();let reads=0;choose(csvFile('huge.csv','',{size:2_000_001,text:()=>{reads++;return Promise.resolve(simple('Huge'));}}));
  await flush();const html=render();assert.equal(reads,0);assert.match(html,/File tối đa 2 MB/);assert.ok(!hasButton('Nhập vào danh sách'));
});

for(const[description,content,message]of[
  ['header-only','name,phone\n',/1 đến 1000/],
  ['duplicate headers','name,name\nOne,Two',/không lặp tên cột/],
  ['unknown header','name,unexpected\nOne,Two',/Dùng CSV có cột/],
  ['empty name','name,phone\n,0900000000',/tên nhóm khách/],
  ['too long name',`name\n${'N'.repeat(161)}`,/tên nhóm khách/],
  ['unclosed quote','name\n"Unclosed',/chưa đóng/],
  ['misplaced quote','name\nUn"quoted"',/ngoặc kép không hợp lệ/],
  ['characters after closing quote','name\n"One"X',/ký tự thừa/],
  ['extra cells','name\nOne,Two',/nhiều ô hơn/],
  ['zero party','name,partySize\nOne,0',/số nguyên từ 1 đến 20/],
  ['fractional party','name,partySize\nOne,1.5',/số nguyên từ 1 đến 20/],
  ['negative party','name,partySize\nOne,-1',/số nguyên từ 1 đến 20/],
  ['too large party','name,partySize\nOne,21',/số nguyên từ 1 đến 20/],
  ['not numeric party','name,partySize\nOne,abc',/số nguyên từ 1 đến 20/],
  ['unknown status','name,status\nOne,Unknown',/trạng thái phản hồi không hợp lệ/],
  ['unknown side','name,side\nOne,Unknown',/phía gia đình/],
  ['too long phone',`name,phone\nOne,${'0'.repeat(31)}`,/vượt giới hạn/],
  ['too many rows',`name\n${Array.from({length:1001},(_,i)=>'Group '+i).join('\n')}`,/1 đến 1000/],
])test(`malformed ${description} produces no submit-ready partial preview`,async()=>{
  reset();render();choose(csvFile('bad.csv',content));await flush();const html=render();
  assert.match(html,message);assert.ok(!hasButton('Nhập vào danh sách'));assert.equal(state.posts.length,0);
});

test('BOM, quoted comma/newline/escaped quote and optional columns preserve valid guest payloads',async()=>{
  reset();const content='\uFEFFname,phone,partySize,notes\r\n"Nhóm, A",0900000000,3,"Ghi chú dòng 1\nDòng 2 có ""ngoặc kép"""\r\nNhóm B,,,';
  await preview(csvFile('quoted.csv',content));button('Nhập vào danh sách').onClick();
  assert.equal(state.posts[0].path,'guests/import');const rows=state.posts[0].data.guests;
  assert.equal(rows.length,2);assert.equal(rows[0].name,'Nhóm, A');assert.equal(rows[0].phone,'0900000000');assert.equal(rows[0].partySize,3);
  assert.equal(rows[0].notes,'Ghi chú dòng 1\nDòng 2 có "ngoặc kép"');assert.equal(rows[1].partySize,1);assert.equal(rows[1].side,'Shared');assert.equal(rows[1].status,'Pending');
});

test('submission locks synchronously; duplicate callbacks, cancellation and file replacement cannot alter it',async()=>{
  reset();await preview(csvFile('A.csv',simple('Nhóm A')));const submit=button('Nhập vào danh sách').onClick;
  const cancel=button('Hủy nhập CSV').onClick;const oldFileInput=fileInput();
  submit();submit();cancel();oldFileInput.onChange({target:{files:[csvFile('B.csv',simple('Nhóm B'))],value:'B.csv'}});
  assert.equal(state.posts.length,1);assert.equal(state.posts[0].data.guests[0].name,'Nhóm A');
  render();assert.equal(button('Nhập vào danh sách').disabled,true);assert.equal(button('Hủy nhập CSV').disabled,true);assert.equal(fileInput().disabled,true);
  settle();const html=render();assert.ok(!hasButton('Nhập vào danh sách'));assert.match(html,/Đã nhập 1 nhóm khách/);assert.equal(fileInput().disabled,false);
});

test('server failure preserves the current preview for a deliberate retry and never retries automatically',async()=>{
  reset();await preview();button('Nhập vào danh sách').onClick();settle({response:{data:{message:'QA IMPORT FAILED'}}});const html=render();
  assert.match(html,/QA IMPORT FAILED/);assert.ok(hasButton('Nhập vào danh sách'));assert.equal(state.posts.length,1);
  button('Nhập vào danh sách').onClick();assert.equal(state.posts.length,2);assert.deepEqual(state.posts[1],state.posts[0]);
});

test('cached read failure denies retained import, exposes read retry and preserves preview for recovery',async()=>{
  reset();await preview();const retained=button('Nhập vào danh sách').onClick;
  state.query.isError=true;state.query.error=new Error('QA READ FAILED');render();retained();
  assert.equal(state.posts.length,0);button('Thử tải lại khách mời').onClick();assert.equal(state.refetches,1);assert.equal(state.posts.length,0);
  state.query.isError=false;render();assert.ok(hasButton('Nhập vào danh sách'));assert.equal(button('Nhập vào danh sách').disabled,false);
});

test('refreshing data or losing edit role prevents an old import callback from writing',async()=>{
  for(const change of [()=>{state.query.isFetching=true;},()=>{state.query.data.myRole='Viewer';},()=>{state.query.data.myRole='Unknown';}]){
    reset();await preview();const retained=button('Nhập vào danh sách').onClick;change();render();retained();assert.equal(state.posts.length,0);
    if(state.query.data.myRole==='Owner')assert.equal(button('Nhập vào danh sách').disabled,true);else assert.ok(!hasButton('Nhập vào danh sách'));
  }
});
