import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { ADMIN_CREATABLE_ROLES, adminCreateUserRequest, administrativeActorIsCurrent, validateAdminUserCreation } from '../src/features/admin/adminCreateUserModel.ts';
import { EMPTY_REGISTRATION } from '../src/features/auth/components/registrationValidation.ts';

const valid = () => ({...EMPTY_REGISTRATION,fullName:' QA Actor ',email:' ACTOR@EXAMPLE.INVALID ',phoneNumber:'0901234567',password:'Synthetic-Password-2026',confirmPassword:'Synthetic-Password-2026',brandName:' QA Shop ',city:'Hà Nội'});
for (const role of ADMIN_CREATABLE_ROLES) test(`Admin creation allows ${role} with only relevant fields`,()=>{
  const values=valid();
  assert.deepEqual(validateAdminUserCreation(values,role,role==='SuperAdmin'),{});
  const payload=adminCreateUserRequest({...values,commissionRate:.12,isVerified:true,partnershipPolicyAccepted:true},role,role==='SuperAdmin');
  assert.equal(payload.role,role);assert.equal(payload.fullName,'QA Actor');assert.equal(payload.email,'actor@example.invalid');
  assert.equal(payload.password,values.password);
  assert.deepEqual(Object.keys(payload).sort(),['fullName','email','phoneNumber','password','role',...(role==='VendorOwner'?['brandName','city']:[]),...(role==='SuperAdmin'?['confirmSuperAdminCreation']:[])].sort());
  if(role==='SuperAdmin')assert.equal(payload.confirmSuperAdminCreation,true);
});
test('Vendor admin provisioning allows name and password only, optional contacts and areas, never impersonates consent',()=>{
  const values={...valid(),partnershipPolicyAccepted:false};
  assert.deepEqual(validateAdminUserCreation(values,'VendorOwner',false),{});
  assert.ok(validateAdminUserCreation({...values,brandName:'x'.repeat(201)},'VendorOwner',false).brandName);
  assert.ok(validateAdminUserCreation({...values,city:'Unknown'},'VendorOwner',false).city);
  assert.equal('partnershipPolicyAccepted' in adminCreateUserRequest(values,'VendorOwner',false),false);
  const minimal={...valid(),email:'',phoneNumber:'',brandName:'',city:''};
  assert.deepEqual(validateAdminUserCreation(minimal,'VendorOwner',false),{});
  const minimalPayload=adminCreateUserRequest(minimal,'VendorOwner',false);
  assert.equal(minimalPayload.fullName,'QA Actor');
  assert.equal(minimalPayload.brandName,'QA Actor');
  assert.equal(minimalPayload.email,'');
  assert.equal(minimalPayload.phoneNumber,'');
  assert.equal(minimalPayload.city,'');
  const nationwidePayload=adminCreateUserRequest({...values,nationwide:true,operatingRegions:[]},'VendorOwner',false);
  assert.equal(nationwidePayload.nationwide,true);
  assert.deepEqual(nationwidePayload.operatingRegions,[]);
});
test('SuperAdmin grant requires explicit confirmation and strong byte-bounded password',()=>{
  assert.ok(validateAdminUserCreation(valid(),'SuperAdmin',false).confirmSuperAdminCreation);
  assert.ok(validateAdminUserCreation({...valid(),password:'12345678901',confirmPassword:'12345678901'},'SuperAdmin',true).password);
  const multiByte='ầ'.repeat(25);
  assert.ok(validateAdminUserCreation({...valid(),password:multiByte,confirmPassword:multiByte},'SuperAdmin',true).password);
  for(const role of ['superadmin','6','__proto__','Unknown'])assert.throws(()=>adminCreateUserRequest(valid(),role,true));
});
test('creation draft is bound to exact administrator, token and session revision',()=>{
  const actor={id:'admin-fixture',token:'synthetic-token',revision:3};
  const current={user:{id:'admin-fixture',role:'SuperAdmin'},token:'synthetic-token',sessionRevision:3};
  assert.equal(administrativeActorIsCurrent(actor,current),true);
  for(const change of [{user:null},{user:{id:'other',role:'SuperAdmin'}},{user:{id:'admin-fixture',role:'Moderator'}},{token:'rotated'},{sessionRevision:4}])
    assert.equal(administrativeActorIsCurrent(actor,{...current,...change}),false);
});

// Exercise the actual form's callbacks; API transport and hooks are isolated so
// no real credentials/accounts/mail/database are involved in these state tests.
const key=Symbol.for('wedding.tests.admin-user-creation');
const moduleUrl=source=>`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`;
const adapters=moduleUrl(`
const get=()=>globalThis[Symbol.for('wedding.tests.admin-user-creation')];
export function useState(initial){const s=get(),i=s.stateIndex++;if(!(i in s.states))s.states[i]=typeof initial==='function'?initial():initial;return[s.states[i],next=>{s.states[i]=typeof next==='function'?next(s.states[i]):next;}];}
export function useRef(initial){const s=get(),i=s.refIndex++;return s.refs[i]??=( {current:initial} );}
export function useEffect(effect){const s=get();if(!s.mounted){s.cleanup=effect();s.mounted=true;}}
export const useQueryClient=()=>({invalidateQueries:value=>{get().invalidations.push(value);}});
export const useAuthStore={getState:()=>get().session};
export function useMutation(options){const s=get();return {get isPending(){return s.pending;},mutateAsync:async body=>{s.pending=true;const result=await options.mutationFn(body);options.onSuccess?.(result);return result;},reset:()=>{s.pending=false;}};}
export const axiosClient={post:(url,body,options)=>{const s=get();s.requests.push({url,body,options});return s.transport(body);}};
export function ProvinceSelect(){return null;}
export function ComboDialog(){return null;}
export const userKeys={all:['users']};
`);
const source=await readFile(new URL('../src/features/admin/UserManagement.tsx',import.meta.url),'utf8');
const snippet=source.slice(source.indexOf('export function UserCreationForm'),source.indexOf('export default function UserManagement'));
let javascript=ts.transpileModule(`
import {useState,useRef,useEffect,useQueryClient,useMutation,useAuthStore,axiosClient,ProvinceSelect,ComboDialog,userKeys} from '${adapters}';
import {UserPlus,Eye,EyeOff,Loader2} from 'lucide-react';
import {EMPTY_REGISTRATION,registrationApiError} from '${new URL('../src/features/auth/components/registrationValidation.ts',import.meta.url).href}';
import {ADMIN_CREATABLE_ROLES,adminRoleNames,adminCreateUserRequest,administrativeActorIsCurrent,validateAdminUserCreation} from '${new URL('../src/features/admin/adminCreateUserModel.ts',import.meta.url).href}';
${snippet}`,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX}}).outputText;
for(const name of ['react/jsx-runtime','lucide-react']){
  const resolved=`from "${import.meta.resolve(name)}"`;
  javascript=javascript.replaceAll(`from "${name}"`,resolved).replaceAll(`from '${name}'`,resolved);
}
const {UserCreationForm}=await import(moduleUrl(javascript));
const nodes=(node,predicate)=>!node||typeof node!=='object'?[]:Array.isArray(node)?node.flatMap(child=>nodes(child,predicate)):[...(predicate(node)?[node]:[]),...nodes(node.props?.children,predicate)];
const text=node=>typeof node==='string'||typeof node==='number'?String(node):Array.isArray(node)?node.map(text).join(''):node&&typeof node==='object'?text(node.props?.children):'';
function harness(){
  const s=globalThis[key]={states:[],refs:[],stateIndex:0,refIndex:0,mounted:false,pending:false,requests:[],invalidations:[],created:[],cancelled:0,session:{user:{id:'fixture-admin',role:'SuperAdmin'},token:'fixture-token',sessionRevision:1},transport:async body=>({data:{id:'new-fixture-user',email:body.email,role:body.role}})};
  const render=()=>{s.stateIndex=0;s.refIndex=0;return UserCreationForm({onCreated:user=>s.created.push(user),onCancel:()=>s.cancelled++});};
  const input=(name,value)=>{const field=nodes(render(),n=>n.type==='input'&&n.props.name===name)[0];assert.ok(field,name);field.props.onChange({target:{value}});};
  const select=role=>nodes(render(),n=>n.type==='select'&&n.props.id==='create-user-role')[0].props.onChange({target:{value:role}});
  const fill=()=>{for(const name of ['fullName','email','phoneNumber','password','confirmPassword'])input(name,valid()[name]);};
  const submit=()=>nodes(render(),n=>n.type==='form')[0].props.onSubmit({preventDefault(){}});
  const button=label=>nodes(render(),n=>n.type==='button'&&text(n)===label)[0];
  const consent=()=>nodes(render(),n=>n.props?.id==='create-user-confirm-admin')[0].props.onChange({target:{checked:true}});
  return{s,render,input,select,fill,submit,button,consent};
}
test('actual form previews and cancels SuperAdmin grant without sending a request',async()=>{
  const h=harness();h.fill();h.select('SuperAdmin');h.consent();await h.submit();
  assert.equal(h.s.requests.length,0);assert.ok(h.button('Xác nhận tạo SuperAdmin'));
  h.button('Hủy').props.onClick();assert.equal(h.s.requests.length,0);
  assert.equal(nodes(h.render(),n=>n.type==='input'&&n.props.name==='email')[0].props.value,valid().email);
});
test('actual grant confirmation is single-flight and preserves creating Admin session',async()=>{
  const h=harness();h.fill();h.select('SuperAdmin');h.consent();await h.submit();
  const before={...h.s.session};let resolve;
  h.s.transport=body=>new Promise(done=>{resolve=()=>done({data:{id:'new-admin',email:body.email,role:body.role}});});
  const confirm=h.button('Xác nhận tạo SuperAdmin');confirm.props.onClick();confirm.props.onClick();
  assert.equal(h.s.requests.length,1);assert.equal(h.s.requests[0].url,'/api/users');
  assert.equal(h.s.requests[0].body.confirmSuperAdminCreation,true);
  assert.equal(h.s.requests[0].options.headers.Authorization,'Bearer fixture-token');
  resolve();await new Promise(setImmediate);
  assert.equal(h.s.created.length,1);assert.deepEqual(h.s.session,before);
  assert.equal(nodes(h.render(),n=>n.type==='input'&&n.props.name==='password')[0].props.value,'');
});
test('stale Admin session cannot confirm a privileged draft',async()=>{
  const h=harness();h.fill();h.select('SuperAdmin');h.consent();await h.submit();h.s.session.sessionRevision++;
  h.button('Xác nhận tạo SuperAdmin').props.onClick();await new Promise(setImmediate);
  assert.equal(h.s.requests.length,0);assert.equal(h.s.created.length,0);assert.match(text(h.render()),/Phiên quản trị đã thay đổi/);
});
test('duplicate-email failure retains draft for a corrected retry',async()=>{
  const h=harness();h.fill();h.s.transport=async()=>{throw{response:{data:{message:'Email đã được sử dụng'}}};};await h.submit();
  assert.equal(h.s.created.length,0);assert.match(text(h.render()),/Email đã được sử dụng/);
  assert.equal(nodes(h.render(),n=>n.type==='input'&&n.props.name==='fullName')[0].props.value,valid().fullName);
  h.input('email','corrected@example.invalid');h.s.transport=async body=>({data:{id:'created',email:body.email,role:body.role}});await h.submit();
  assert.equal(h.s.created.length,1);assert.equal(h.s.requests.length,2);
});
