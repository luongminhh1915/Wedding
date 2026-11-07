import { test } from 'node:test';
import assert from 'node:assert/strict';
import { managedVerificationPath, legacyVerificationHref, verificationActorIsCurrent, validateVerificationFiles, uploadVerificationFiles, fetchVerificationDocument } from '../src/features/journey/verificationDocumentModel.ts';

const owner = '11111111-1111-1111-1111-111111111111';
const path = `/uploads/documents/${owner}/0123456789abcdef0123456789abcdef.pdf`;
const file = () => new File(['synthetic PDF'], 'legal.pdf', { type: 'application/pdf' });
test('Private references require the expected owner, canonical file and no URL token', () => {
  assert.equal(managedVerificationPath(path, owner), path);
  for (const bad of [path+'?token=secret', path.replace(owner,'22222222-2222-2222-2222-222222222222'), '/uploads/documents/0123456789abcdef0123456789abcdef.pdf', '/uploads/listings/a.png', path.replace('.pdf','.svg'), path+'/../web.config']) assert.equal(managedVerificationPath(bad, owner), null);
  assert.equal(legacyVerificationHref('https://legacy.example.invalid/legal.pdf'), 'https://legacy.example.invalid/legal.pdf');
  for (const bad of ['http://external.example.invalid/a.pdf','https://user:secret@example.invalid/a.pdf','javascript:alert(1)','//external.example.invalid/a.pdf']) assert.equal(legacyVerificationHref(bad), null);
});
test('Actor guard rejects login changes, role changes, refreshed token and same-user ABA', () => {
  const actor={id:'owner',role:'VendorOwner',token:'token-one',revision:4};
  const session={user:{id:'owner',role:'VendorOwner'},token:'token-one',sessionRevision:4};
  assert.equal(verificationActorIsCurrent(actor,session),true);
  for(const changed of [{...session,token:'new-token'},{...session,sessionRevision:6},{...session,user:{id:'other',role:'VendorOwner'}},{...session,user:{id:'owner',role:'SuperAdmin'}},{...session,user:null,token:null}]) assert.equal(verificationActorIsCurrent(actor,changed),false);
});
test('Upload preflight checks individual size, combined size, count and MIME', () => {
  assert.doesNotThrow(()=>validateVerificationFiles([file()]));
  for(const bad of [[{name:'x.pdf',type:'text/html',size:30}],[{name:'x.svg',type:'image/svg+xml',size:30}],[{name:'x.doc',type:'application/msword',size:21*1024*1024}],Array.from({length:2},()=>({name:'x.pdf',type:'application/pdf',size:17*1024*1024}))]) assert.throws(()=>validateVerificationFiles(bad));
  assert.throws(()=>validateVerificationFiles([file()],10));
});
test('Upload does not commit response after actor switches or cancellation', async()=>{
  let current=true;
  await assert.rejects(()=>uploadVerificationFiles([file()],owner,{existingCount:0,signal:new AbortController().signal,isCurrent:()=>current,send:async()=>{current=false;return[{url:path,originalName:'legal.pdf'}];}}));
  const abort=new AbortController();
  await assert.rejects(()=>uploadVerificationFiles([file()],owner,{existingCount:0,signal:abort.signal,isCurrent:()=>true,send:async()=>{abort.abort();return[{url:path,originalName:'legal.pdf'}];}}));
});
test('Upload accepts only complete saved owner responses, never blob fallback',async()=>{
  const options={existingCount:0,signal:new AbortController().signal,isCurrent:()=>true};
  for(const value of [[],[{url:'blob:preview',originalName:'legal.pdf'}],[{url:path.replace(owner,'22222222-2222-2222-2222-222222222222'),originalName:'legal.pdf'}]]) await assert.rejects(()=>uploadVerificationFiles([file()],owner,{...options,send:async()=>value}));
  const result=await uploadVerificationFiles([file()],owner,{...options,send:async()=>[{url:path,originalName:'legal.pdf'}]});assert.equal(result[0].url,path);
});
test('Private download uses canonical path and discards a stale actor response',async()=>{
  let requested='';let current=true;
  await assert.rejects(()=>fetchVerificationDocument(path,owner,{signal:new AbortController().signal,isCurrent:()=>current,send:async(url)=>{requested=url;current=false;return new Blob(['private']);}}));
  assert.equal(requested,path);
  await assert.rejects(()=>fetchVerificationDocument('https://external.example.invalid/a.pdf',owner,{signal:new AbortController().signal,isCurrent:()=>true,send:async()=>{throw Error('must not send authorization externally');}}));
});
