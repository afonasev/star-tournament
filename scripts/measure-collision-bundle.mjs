import { brotliCompressSync, gzipSync } from 'node:zlib'
import { readFileSync, readdirSync } from 'node:fs'
import { basename, join } from 'node:path'

const assetsDirectory = new URL('../dist/assets/', import.meta.url)
const javascriptFiles = readdirSync(assetsDirectory)
  .filter((file) => file.endsWith('.js'))
  .sort()

const chunks = javascriptFiles.map((file) => {
  const bytes = readFileSync(join(assetsDirectory.pathname, file))
  return {
    category: file.startsWith('rapier-') ? 'rapier-lazy' : 'main',
    file: basename(file),
    rawBytes: bytes.byteLength,
    gzipBytes: gzipSync(bytes, { level: 9 }).byteLength,
    brotliBytes: brotliCompressSync(bytes).byteLength,
  }
})

const rapierChunks = chunks.filter((chunk) => chunk.category === 'rapier-lazy')
const mainChunks = chunks.filter((chunk) => chunk.category === 'main')
if (rapierChunks.length !== 1 || mainChunks.length === 0) {
  throw new Error('Expected exactly one Rapier lazy chunk and at least one main chunk')
}

process.stdout.write(`${JSON.stringify({
  schemaVersion: 1,
  compatibilityIdentity: {
    contractVersion: 'collision-contract-v1',
    backendPackage: '@dimforge/rapier3d-deterministic-compat',
    backendVersion: '0.20.0',
  },
  initialMainIncludesRapierRuntime: false,
  chunks,
  initLatencyMilliseconds: {
    cold: null,
    warm: null,
    source: 'browser diagnostics',
  },
}, null, 2)}\n`)
