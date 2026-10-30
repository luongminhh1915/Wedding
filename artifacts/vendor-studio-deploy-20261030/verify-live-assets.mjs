import { readFile, readdir, writeFile } from 'node:fs/promises';
import { resolve, relative, sep } from 'node:path';
import { createHash } from 'node:crypto';
import https from 'node:https';
const root = resolve('artifacts/vendor-studio-release-20261030/wedding-match-20261029-193643-936106ca/site/wwwroot');
const resultsFile = resolve('artifacts/vendor-studio-deploy-20261030/live-assets.json');
async function walk(dir) {
  const files=[];
  for(const e of await readdir(dir,{withFileTypes:true})) {
    const path=resolve(dir,e.name);
    if(e.isDirectory())files.push(...await walk(path));
    else if(e.isFile())files.push(path);
  }
  return files;
}
function download(path) {
  return new Promise((resolve,reject)=>{
    const req=https.get({hostname:'weddingmatch.website',path,timeout:25000},r=>{
      const chunks=[];
      r.on('data',c=>chunks.push(c));
      r.on('end',()=>resolve({status:r.statusCode,bytes:Buffer.concat(chunks)}));
      r.on('error',reject);
    });
    req.on('error',reject); req.on('timeout',()=>req.destroy(new Error('timeout')));
  });
}
const files=await walk(root),results=[];
let next=0;
async function worker() {
  while(next<files.length) {
    const file=files[next++];
    const path='/'+relative(root,file).split(sep).map(encodeURIComponent).join('/');
    try {
      const local=await readFile(file),remote=await download(path);
      const localSha=createHash('sha256').update(local).digest('hex');
      const remoteSha=createHash('sha256').update(remote.bytes).digest('hex');
      results.push({path,status:remote.status,bytes:local.length,match:remote.status===200&&localSha===remoteSha});
    } catch(e) {results.push({path,match:false,error:String(e)});}
  }
}
await Promise.all(Array.from({length:4},worker));
const result={utc:new Date().toISOString(),total:results.length,matched:results.filter(x=>x.match).length,results:results.sort((a,b)=>a.path.localeCompare(b.path))};
await writeFile(resultsFile,JSON.stringify(result,null,2));
console.log(JSON.stringify({total:result.total,matched:result.matched,failures:result.results.filter(x=>!x.match)}));
if(result.matched!==result.total)process.exitCode=1;
