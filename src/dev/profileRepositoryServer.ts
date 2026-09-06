import { createHash, randomUUID } from 'node:crypto'
import { mkdir, open, readFile, rename, unlink } from 'node:fs/promises'
import { resolve } from 'node:path'

import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Plugin } from 'vite'

const API = '/__star-tournament/profiles'
const versionOf = (text: string) => createHash('sha256').update(text).digest('hex')
const send = (response: ServerResponse, status: number, value: unknown) => { response.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); response.end(JSON.stringify(value)) }

async function atomicWrite(path: string, value: unknown, expectedVersion: string, current: string) {
  if (expectedVersion !== 'missing' && versionOf(current) !== expectedVersion) throw new Error('Файл профилей изменён на диске. Перезагрузите страницу.')
  const temporary = `${path}.${randomUUID()}.tmp`
  try {
    const handle = await open(temporary, 'wx', 0o600)
    try { await handle.writeFile(`${JSON.stringify(value, null, 2)}\n`); await handle.sync() } finally { await handle.close() }
    await rename(temporary, path)
  } finally { await unlink(temporary).catch(() => undefined) }
}

export function profileRepositoryPlugin(root: string): Plugin {
  const releasePath = resolve(root, 'balance/releases.json')
  const historyPath = resolve(root, '.local/balance-profiles.json')
  return {
    name: 'star-tournament-profile-repository',
    configureServer(server) {
      server.middlewares.use(async (request: IncomingMessage, response: ServerResponse, next: () => void) => {
        if (request.url?.split('?')[0] !== API) return next()
        try {
          if (request.headers.origin && request.headers.origin !== `http://${request.headers.host}`) return send(response, 403, { error: 'Чужой origin' })
          const validators = await server.ssrLoadModule('/src/profiles/localRevisions.ts') as typeof import('../profiles/localRevisions')
          const profiles = await server.ssrLoadModule('/src/profiles/gameDesignProfile.ts') as typeof import('../profiles/gameDesignProfile')
          const text = await readFile(releasePath, 'utf8')
          const parsed = validators.loadReleaseManifest(JSON.parse(text))
          if (!parsed.ok) throw new Error(parsed.message)
          let historyText: string | null = null
          try { historyText = await readFile(historyPath, 'utf8') } catch (error) { if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error }
          const history = historyText === null ? { schemaVersion: 2 as const, profiles: {}, selected: null } : JSON.parse(historyText)
          const validHistory = validators.validateLocalProfileCatalog(history)
          if (!validHistory.ok) throw new Error(validHistory.message)
          if (request.method === 'GET') return send(response, 200, { manifest: parsed.manifest, version: versionOf(text), history: validHistory.catalog, historyVersion: historyText === null ? 'missing' : versionOf(historyText) })
          if (request.method !== 'POST' || request.headers['content-type']?.split(';')[0] !== 'application/json') return send(response, 405, { error: 'Ожидался JSON POST' })
          const chunks: Buffer[] = []
          for await (const chunk of request) chunks.push(Buffer.from(chunk))
          const body = JSON.parse(Buffer.concat(chunks).toString()) as { operation?: string; revision?: unknown; history?: unknown; version?: unknown }
          if (typeof body.version !== 'string') throw new Error('Не передана версия каталога.')
          if (body.operation === 'saveHistory') {
            const candidate = validators.validateLocalProfileCatalog(body.history)
            if (!candidate.ok) throw new Error(candidate.message)
            const currentVersion = historyText === null ? 'missing' : versionOf(historyText)
            if (body.version !== currentVersion) throw new Error('История профилей изменилась на диске. Перезагрузите страницу.')
            await mkdir(resolve(root, '.local'), { recursive: true })
            const currentText = historyText ?? ''
            await atomicWrite(historyPath, candidate.catalog, currentVersion, currentText)
            return send(response, 200, { history: candidate.catalog })
          }
          if (body.operation !== 'publish') throw new Error('Неизвестная операция.')
          if (body.version !== versionOf(text)) throw new Error('Релиз изменён на диске. Перезагрузите страницу.')
          const checked = profiles.validateGameDesignProfile(body.revision)
          if (!checked.ok) throw new Error('Публикуемая ревизия невалидна.')
          const revision = checked.profile
          const sameNumber = parsed.manifest.revisions.find((entry) => entry.id === revision.id && entry.revision === revision.revision)
          if (sameNumber && JSON.stringify(sameNumber) !== JSON.stringify(revision)) throw new Error('Опубликованную ревизию нельзя изменить.')
          const manifest = { schemaVersion: 1 as const, releaseRef: validators.profileRevisionRef(revision), revisions: sameNumber ? parsed.manifest.revisions : [...parsed.manifest.revisions, revision] }
          const manifestCheck = validators.loadReleaseManifest(manifest)
          if (!manifestCheck.ok) throw new Error(manifestCheck.message)
          await atomicWrite(releasePath, manifest, body.version, text)
          return send(response, 200, { manifest, version: versionOf(JSON.stringify(manifest, null, 2) + '\n') })
        } catch (error) { return send(response, 409, { error: error instanceof Error ? error.message : 'Не удалось опубликовать ревизию.' }) }
      })
    },
  }
}
