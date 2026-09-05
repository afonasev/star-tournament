import { execFileSync } from 'node:child_process'
import { mkdtemp, writeFile, mkdir, readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

const args=process.argv.slice(2)
const option=(name,fallback)=>{const i=args.indexOf('--'+name);return i<0?fallback:args[i+1]}
await mkdir('.ai-cache',{recursive:true})
const directory=await mkdtemp(resolve('.ai-cache/run-'))
const localBundle=join(directory,'runner.mjs')
execFileSync(process.execPath,['node_modules/rolldown/bin/cli.mjs','src/ai/headless.ts','--file',localBundle,'--format','esm','--platform','node','--external','@dimforge/rapier3d-deterministic-compat'],{stdio:'pipe'})
const {botConfiguration,runAiMatch,createAiRunner,parsePlayableSimulationReplay,simulationSnapshotHash}=await import(pathToFileURL(localBundle).href)
const replayPath=option('replay',null)
if(replayPath){
  const replay=parsePlayableSimulationReplay(await readFile(replayPath,'utf8'))
  const runner=await createAiRunner(replay.configuration,replay.arenaDefinition,replay.initialSnapshot)
  try { for(const frame of replay.frames) { if(frame.tick>Number(option('ticks','Infinity'))) break; runner.step(frame) }; if(option('output',null)) await writeFile(option('output',null),JSON.stringify(runner.snapshot)); console.log(JSON.stringify({ticks:runner.snapshot.tick,finalHash:simulationSnapshotHash(runner.snapshot,replay.configuration)})) }
  finally {runner.dispose()}
  process.exit(0)
}
const seed=Number(option('seed','101'));const mode=option('mode','ffa');const size=option('size','small')
const levels=option('levels','easy,normal,hard').split(',')
const output=option('output',null)
const result=await runAiMatch(botConfiguration(seed,levels,mode,size),Number(option('ticks','7200')))
if(output){await mkdir(output,{recursive:true});await writeFile(join(output,'summary.json'),JSON.stringify(result.summary,null,2));await writeFile(join(output,'replay.json'),JSON.stringify(result.replay));await writeFile(join(output,'snapshot.json'),JSON.stringify(result.snapshot))}
console.log(JSON.stringify(result.summary,null,2))
