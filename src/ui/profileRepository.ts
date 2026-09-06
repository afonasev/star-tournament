import { type LocalProfileCatalog, type ReleaseManifest, type ValidatedGameDesignProfile } from '../profiles'

const API = '/__star-tournament/profiles'
export const profileFilesystemAvailable = (import.meta as unknown as { readonly env: { readonly DEV: boolean } }).env.DEV

interface RepositoryRead { readonly manifest: ReleaseManifest; readonly version: string; readonly history: LocalProfileCatalog; readonly historyVersion: string }
async function readRepository(): Promise<RepositoryRead> {
  const response = await fetch(API, { cache: 'no-store' })
  const data = await response.json() as Partial<RepositoryRead> & { error?: string }
  if (!response.ok || !data.manifest || !data.version || !data.history || !data.historyVersion) throw new Error(data.error ?? 'Файловое хранилище профилей недоступно.')
  return data as RepositoryRead
}
export async function loadRepositoryHistory(): Promise<LocalProfileCatalog | null> {
  if (!profileFilesystemAvailable) return null
  return (await readRepository()).history
}
export async function persistProfileHistory(history: LocalProfileCatalog): Promise<void> {
  if (!profileFilesystemAvailable) return
  const current = await readRepository()
  const response = await fetch(API, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ operation: 'saveHistory', history, version: current.historyVersion }) })
  const saved = await response.json() as { error?: string }
  if (!response.ok) throw new Error(saved.error ?? 'Не удалось сохранить локальную историю профилей.')
}

export async function publishProfileRevision(revision: ValidatedGameDesignProfile): Promise<ReleaseManifest> {
  if (!profileFilesystemAvailable) throw new Error('Публикация доступна только на dev-стенде.')
  const current = await readRepository()
  const response = await fetch(API, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ operation: 'publish', revision, version: current.version }) })
  const saved = await response.json() as { manifest?: ReleaseManifest; error?: string }
  if (!response.ok || !saved.manifest) throw new Error(saved.error ?? 'Не удалось опубликовать ревизию.')
  return saved.manifest
}
