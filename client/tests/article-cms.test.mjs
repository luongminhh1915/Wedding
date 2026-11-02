import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { ARTICLE_IMAGE_MAX_BYTES, articleImageSrc, articleSlug, isCurrentCmsActor, requireUploadedArticleImage, validateArticleImageFile } from '../src/features/journey/admin/articleCmsModel.ts';

const actor={id:'cms-admin',role:'SuperAdmin',token:'synthetic-cms-token',revision:2};
const session=()=>({user:{id:actor.id,role:actor.role},token:actor.token,sessionRevision:actor.revision});
const managed='/uploads/articles/0123456789abcdef0123456789abcdef.webp';

test('article image selection rejects unsupported and over-limit files before upload',()=>{
  assert.equal(validateArticleImageFile({name:'Wedding.JPG',type:'image/jpeg',size:ARTICLE_IMAGE_MAX_BYTES}), '');
  for(const file of [{name:'a.svg',type:'image/svg+xml',size:50},{name:'a.png',type:'text/html',size:50},{name:'a.jpg',type:'image/jpeg',size:0},{name:'a.webp',type:'image/webp',size:ARTICLE_IMAGE_MAX_BYTES+1}])
    assert.notEqual(validateArticleImageFile(file),'');
});
test('article image responses require managed paths and public previews keep API origins and legacy HTTPS',()=>{
  assert.equal(requireUploadedArticleImage(managed),managed);
  for(const path of ['https://other.invalid/cover.jpg','/uploads/articles/../../private/config.json','/uploads/invitations/a.jpg','javascript:alert(1)',null])assert.throws(()=>requireUploadedArticleImage(path));
  assert.equal(articleImageSrc(managed,'https://api.example.invalid/'),'https://api.example.invalid'+managed);
  assert.equal(articleImageSrc('https://photos.example.invalid/legacy.jpg','https://api.example.invalid'),'https://photos.example.invalid/legacy.jpg');
  assert.equal(articleImageSrc('javascript:alert(1)'), '');
  assert.equal(articleImageSrc('//other.invalid/cover.png'), '');
});
test('CMS editor session is bound to exact permitted actor, token and revision',()=>{
  assert.equal(isCurrentCmsActor(actor,session()),true);
  assert.equal(isCurrentCmsActor({...actor,role:'Moderator'},{...session(),user:{id:actor.id,role:'Moderator'}}),true);
  for(const update of [{user:null},{user:{id:'other',role:'SuperAdmin'}},{user:{id:actor.id,role:'Customer'}},{token:'different'},{sessionRevision:3}])assert.equal(isCurrentCmsActor(actor,{...session(),...update}),false);
});
test('Vietnamese article slug generation produces stable shareable paths',()=>{
  assert.equal(articleSlug(' Đám cưới & kỷ niệm của ĐỨC ANH! '),'dam-cuoi-ky-niem-cua-duc-anh');
  assert.equal(articleSlug('❤️'),'');assert.ok(articleSlug('a'.repeat(350)).length<=300);
});

// Actual upload/form callbacks with isolated hooks, image preparation and HTTP:
// no real account, image, API, email or database is involved.
const key=Symbol.for('wedding.tests.article-cms');
const moduleUrl=source=>`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`;
const adapter=moduleUrl(`
const state=()=>globalThis[Symbol.for('wedding.tests.article-cms')];
export function useState(initial){const s=state(),i=s.stateIndex++;if(!(i in s.states))s.states[i]=typeof initial==='function'?initial():initial;return[s.states[i],next=>{s.states[i]=typeof next==='function'?next(s.states[i]):next;}];}
export function useRef(initial){const s=state(),i=s.refIndex++;return s.refs[i]??=({current:initial});}
export function useEffect(effect){const s=state(),i=s.effectIndex++;if(!(i in s.effects))s.effects[i]=effect();}
export function useId(){return 'cms-image-input';}
export const useAuthStore={getState:()=>state().session};
export const axiosClient={post:(url,body,options)=>{const s=state();s.requests.push({method:'POST',url,body,options});return s.transport(body);},put:(url,body,options)=>{const s=state();s.requests.push({method:'PUT',url,body,options});return s.transport(body);}};
export function useMutation(options){const s=state();return{get isPending(){return s.pending;},mutateAsync:async body=>{s.pending=true;return options.mutationFn(body);},reset:()=>{s.pending=false;}};}
export function prepareInvitationImage(file){return state().prepare(file);}
export function apiError(error){return error?.response?.data?.message??error?.message??'Không thực hiện được yêu cầu.';}
export function ErrorText(){return null;}
export function ArticleCoverPicker(){return null;}
`);
async function component(file,start,end,imports){
  const source=await readFile(new URL('../src/features/journey/admin/'+file,import.meta.url),'utf8');
  const snippet=source.slice(source.indexOf(start),end?source.indexOf(end):undefined);
  let javascript=ts.transpileModule(`${imports}\n${snippet}`,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX}}).outputText;
  for(const name of ['react/jsx-runtime','lucide-react'])javascript=javascript.replaceAll(`from "${name}"`,`from "${import.meta.resolve(name)}"`).replaceAll(`from '${name}'`,`from "${import.meta.resolve(name)}"`);
  return import(moduleUrl(javascript));
}
const modelUrl=new URL('../src/features/journey/admin/articleCmsModel.ts',import.meta.url).href;
const {ArticleCoverPicker}=await component('ArticleCoverPicker.tsx','export function ArticleCoverPicker',null,`
import {useState,useRef,useEffect,useId,useAuthStore,axiosClient,apiError,prepareInvitationImage} from '${adapter}';
import {ImagePlus,Loader2,Trash2} from 'lucide-react';
import {articleImageSrc,isCurrentCmsActor,requireUploadedArticleImage,validateArticleImageFile} from '${modelUrl}';
`);
const {ArticleEditorForm}=await component('ArticleCms.tsx','type ArticleDraft','export function ArticleCms()',`
import {useState,useRef,useEffect,useMutation,useAuthStore,axiosClient,apiError,ErrorText,ArticleCoverPicker} from '${adapter}';
import {Loader2} from 'lucide-react';
import {articleSlug,isCurrentCmsActor} from '${modelUrl}';
`);
const nodes=(node,predicate)=>!node||typeof node!=='object'?[]:Array.isArray(node)?node.flatMap(child=>nodes(child,predicate)):[...(predicate(node)?[node]:[]),...nodes(node.props?.children,predicate)];
const text=node=>typeof node==='string'||typeof node==='number'?String(node):Array.isArray(node)?node.map(text).join(''):node&&typeof node==='object'?text(node.props?.children):'';
const flush=()=>new Promise(setImmediate);
function harness(Component,extra={}){
  const s=globalThis[key]={states:[],refs:[],effects:[],stateIndex:0,refIndex:0,effectIndex:0,pending:false,session:session(),requests:[],changes:[],busy:[],saved:[],prepare:async file=>file,transport:async()=>({data:{url:managed}})};
  const props={actor,value:'https://old.example.invalid/cover.jpg',disabled:false,onChange:value=>s.changes.push(value),onUploadingChange:value=>s.busy.push(value),article:null,onSaved:(title,publish)=>s.saved.push({title,publish}),onNew:()=>{},onBusyChange:value=>s.busy.push(value),...extra};
  const render=()=>{s.stateIndex=0;s.refIndex=0;s.effectIndex=0;return Component(props);};
  const input=(name,value)=>{const node=nodes(render(),n=>n.props?.name===name)[0];assert.ok(node,name);node.props.onChange({target:{value,checked:value}});};
  const button=label=>nodes(render(),n=>n.type==='button'&&text(n)===label)[0];
  const choose=file=>nodes(render(),n=>n.type==='input'&&n.props.type==='file')[0].props.onChange({target:{files:[file]}});
  const submit=async()=>{nodes(render(),n=>n.type==='form')[0].props.onSubmit({preventDefault(){}});await flush();};
  return{s,props,render,input,button,choose,submit};
}
const photo=()=>new File(['synthetic image'], 'photo.png',{type:'image/png'});
const fill=h=>{h.input('title','Đám cưới của hai bạn');h.input('content','Nội dung thử nghiệm cho bài viết.');};

test('actual upload picker sends one multipart request and keeps captured CMS authorization',async()=>{
  const h=harness(ArticleCoverPicker);let done;h.s.transport=()=>new Promise(resolve=>{done=resolve;});
  h.choose(photo());h.choose(photo());await flush();assert.equal(h.s.requests.length,1);
  assert.equal(h.s.requests[0].url,'/api/admin/articles/images');assert.ok(h.s.requests[0].body.get('file') instanceof File);
  assert.equal(h.s.requests[0].options.headers.Authorization,'Bearer '+actor.token);
  assert.deepEqual(h.s.busy,[true]);done({data:{url:managed}});await flush();
  assert.deepEqual(h.s.changes,[managed]);assert.deepEqual(h.s.busy,[true,false]);
});
test('invalid image never reaches preparation or HTTP; failed replace preserves previous cover',async()=>{
  const h=harness(ArticleCoverPicker);h.choose(new File(['svg'],'drawing.svg',{type:'image/svg+xml'}));await flush();assert.equal(h.s.requests.length,0);
  h.s.transport=async()=>{throw{response:{data:{message:'Ảnh bị hỏng'}}};};h.choose(photo());await flush();
  assert.equal(h.s.changes.length,0);assert.equal(nodes(h.render(),n=>n.type==='img')[0].props.src,'https://old.example.invalid/cover.jpg');
  assert.match(text(h.render()),/Ảnh hiện tại vẫn được giữ lại/);
});
test('stale CMS actor during image preparation prevents network upload',async()=>{
  const h=harness(ArticleCoverPicker);let resume;h.s.prepare=file=>new Promise(resolve=>{resume=()=>resolve(file);});
  h.choose(photo());h.s.session.sessionRevision++;resume();await flush();assert.equal(h.s.requests.length,0);assert.equal(h.s.changes.length,0);
});
test('stale upload response or unmounted editor cannot replace another draft cover',async()=>{
  const h=harness(ArticleCoverPicker);let done;h.s.transport=()=>new Promise(resolve=>{done=resolve;});
  h.choose(photo());await flush();h.s.effects.forEach(cleanup=>cleanup?.());
  assert.equal(h.s.requests[0].options.signal.aborted,true);done({data:{url:managed}});await flush();assert.equal(h.s.changes.length,0);
});
test('unexpected upload response is rejected without changing current image',async()=>{
  const h=harness(ArticleCoverPicker);h.s.transport=async()=>({data:{url:'https://unexpected.example.invalid/image.png'}});h.choose(photo());await flush();
  assert.equal(h.s.changes.length,0);assert.match(text(h.render()),/đường dẫn ảnh đã lưu hợp lệ/);
});
test('new article auto-slug follows title until manual change; existing slug never silently changes',()=>{
  const h=harness(ArticleEditorForm);h.input('title','Đám cưới đầu tiên');assert.equal(nodes(h.render(),n=>n.props?.name==='slug')[0].props.value,'dam-cuoi-dau-tien');
  h.input('slug','duong-dan-rieng');h.input('title','Tiêu đề khác');assert.equal(nodes(h.render(),n=>n.props?.name==='slug')[0].props.value,'duong-dan-rieng');
  h.button('Gợi ý từ tiêu đề').props.onClick();assert.equal(nodes(h.render(),n=>n.props?.name==='slug')[0].props.value,'tieu-de-khac');
  const edit=harness(ArticleEditorForm,{article:{id:'article1',title:'Cũ',slug:'old-path',category:'Cưới',content:'Nội dung cũ'}});edit.input('title','Tiêu đề mới');assert.equal(nodes(edit.render(),n=>n.props?.name==='slug')[0].props.value,'old-path');
});
test('actual form blocks save during upload and saves managed cover with publish choice',async()=>{
  const h=harness(ArticleEditorForm);fill(h);
  const picker=()=>nodes(h.render(),n=>n.type?.name==='ArticleCoverPicker')[0];
  picker().props.onUploadingChange(true);await h.submit();assert.equal(h.s.requests.length,0);
  picker().props.onChange(managed);picker().props.onUploadingChange(false);h.input('publish',true);await h.submit();
  assert.equal(h.s.requests.length,1);assert.equal(h.s.requests[0].body.thumbnailUrl,managed);assert.equal(h.s.requests[0].body.publish,true);assert.equal(h.s.saved.length,1);
  assert.equal(nodes(h.render(),n=>n.props?.name==='thumbnailUrl').length,0);
});
test('failed save preserves article text and cover for retry; stale session sends nothing',async()=>{
  const h=harness(ArticleEditorForm);fill(h);h.s.transport=async()=>{throw{response:{data:{message:'Slug đã tồn tại'}}};};await h.submit();
  assert.equal(h.s.saved.length,0);assert.match(text(h.render()),/Slug đã tồn tại/);assert.equal(nodes(h.render(),n=>n.props?.name==='title')[0].props.value,'Đám cưới của hai bạn');
  h.s.session.token='other-session';await h.submit();assert.equal(h.s.requests.length,1);assert.match(text(h.render()),/Phiên biên tập đã thay đổi/);
});
