import { Worker, isMainThread, parentPort, workerData } from 'node:worker_threads'
import { execFileSync } from 'node:child_process'
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises'
import { resolve, join } from 'node:path'
import { pathToFileURL } from 'node:url'

if(!isMainThread) {
  const {botConfiguration,runAiMatch}=await import(pathToFileURL(workerData.bundle).href)
  for(const job of workerData.jobs) {
    const result=await runAiMatch(botConfiguration(job.seed,job.levels,job.mode,job.size))
    if(result.summary.outcome!=='finished') {
      const root=join(workerData.output,`failure-${job.id}`);await mkdir(root,{recursive:true})
      await writeFile(join(root,'replay.json'),JSON.stringify(result.replay))
      await writeFile(join(root,'snapshot.json'),JSON.stringify(result.snapshot))
    }
    parentPort.postMessage({job,summary:result.summary})
  }
} else {
  const args=process.argv.slice(2);const arg=(key,fallback)=>{const i=args.indexOf('--'+key);return i<0?fallback:args[i+1]}
  const output=resolve(arg('output','test-results/ai-evaluation'));await mkdir(output,{recursive:true});await mkdir('.ai-cache',{recursive:true})
  const dir=await mkdtemp(resolve('.ai-cache/eval-'));const bundle=join(dir,'runner.mjs')
  execFileSync(process.execPath,['node_modules/rolldown/bin/cli.mjs','src/ai/headless.ts','--file',bundle,'--format','esm','--platform','node','--external','@dimforge/rapier3d-deterministic-compat'],{stdio:'pipe'})
  const {AI_EVALUATION_V1:suite,wilsonInterval}=await import(pathToFileURL(bundle).href)
  const count=Number(arg('seeds',String(suite.pairedSeeds)));const jobs=[]
  for(const [lower,higher] of [['easy','normal'],['normal','hard']]) for(let i=0;i<count;i++) for(const swapped of [false,true]) {
    const seed=suite.evaluationSeedStart+i;const levels=swapped?[higher,lower]:[lower,higher]
    jobs.push({id:jobs.length,seed,levels,mode:'ffa',size:['small','medium','large'][i%3],pair:`${lower}-${higher}`,strongId:swapped?'bot-1':'bot-2'})
  }
  for(const size of ['small','medium','large'])for(const mode of ['ffa','teams'])for(const n of [2,4,8])jobs.push({id:jobs.length,seed:suite.evaluationSeedStart+suite.pairedSeeds+n,levels:Array.from({length:n},(_,i)=>['easy','normal','hard'][i%3]),mode,size,pair:null})
  await writeFile(join(output,'suite.json'),JSON.stringify({suite,jobs},null,2))
  const results=[];const workers=Number(arg('workers','4'))
  await Promise.all(Array.from({length:workers},(_,i)=>new Promise((done,reject)=>{
    const worker=new Worker(new URL(import.meta.url),{workerData:{bundle,output,jobs:jobs.filter((_,j)=>j%workers===i)}})
    worker.on('message',entry=>{results.push(entry);if(results.length%20===0)console.log(`AI evaluation ${results.length}/${jobs.length}`)})
    worker.on('error',reject);worker.on('exit',code=>code===0?done():reject(Error(`Worker exit ${code}`)))
  })))
  results.sort((a,b)=>a.job.id-b.job.id)
  const comparisons=['easy-normal','normal-hard'].map(pair=>{
    const games=results.filter(r=>r.job.pair===pair);const wins=games.filter(r=>r.summary.result?.winnerId===r.job.strongId).length
    const interval=wilsonInterval(wins,games.length)
    return {pair,games:games.length,wins,interval,pass:games.length>=suite.pairedSeeds*2&&interval[0]>suite.requiredWinLowerBound&&games.every(r=>r.summary.outcome==='finished')}
  })
  const failed=results.filter(r=>r.summary.outcome!=='finished').map(r=>({job:r.job,outcome:r.summary.outcome,metrics:r.summary.metrics}))
  const report={suite:suite.id,status:failed.length?'FAIL':comparisons.every(c=>c.pass)?'PASS':'INCONCLUSIVE',comparisons,failed,total:results.length,results}
  await writeFile(join(output,'report.json'),JSON.stringify(report,null,2))
  console.log(JSON.stringify({status:report.status,comparisons,failed:failed.length,total:results.length,output},null,2))
  if(report.status!=='PASS')process.exitCode=1
}
