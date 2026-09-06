import releaseManifestJson from '../../balance/releases.json'

import { computeProfileContentHash, validateGameDesignProfile, type GameDesignProfile, type ValidatedGameDesignProfile } from './gameDesignProfile'

export const GAME_DESIGN_LAB_STORAGE_KEY = 'star-tournament.game-design-lab.v2'

export interface ProfileRevisionRef { readonly id: string; readonly revision: number; readonly contentHash: string }
export interface ReleaseManifest { readonly schemaVersion: 1; readonly releaseRef: ProfileRevisionRef; readonly revisions: readonly GameDesignProfile[] }
export interface LocalProfileCatalog {
  readonly schemaVersion: 2
  readonly profiles: Readonly<Record<string, readonly GameDesignProfile[]>>
  /** Null deliberately means: use the checked-in release revision. */
  readonly selected: ProfileRevisionRef | null
}
export type LocalProfileCatalogResult = { readonly ok: true; readonly catalog: LocalProfileCatalog } | { readonly ok: false; readonly message: string }
const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T

export function sameProfileRevision(left: ProfileRevisionRef | null, right: ProfileRevisionRef | null): boolean {
  return left?.id === right?.id && left?.revision === right?.revision && left?.contentHash === right?.contentHash
}
export function profileRevisionRef(profile: ValidatedGameDesignProfile): ProfileRevisionRef {
  return { id: profile.id, revision: profile.revision, contentHash: profile.contentHash }
}
function validProfile(candidate: unknown, message: string): GameDesignProfile | { readonly ok: false; readonly message: string } {
  const validation = validateGameDesignProfile(candidate)
  return validation.ok ? clone(validation.profile) as GameDesignProfile : { ok: false, message }
}

export function loadReleaseManifest(value: unknown = releaseManifestJson): { readonly ok: true; readonly manifest: ReleaseManifest } | { readonly ok: false; readonly message: string } {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return { ok: false, message: 'Каталог релизов повреждён.' }
  const data = value as Record<string, unknown>
  if (data.schemaVersion !== 1 || !Array.isArray(data.revisions) || data.releaseRef === null || typeof data.releaseRef !== 'object') return { ok: false, message: 'Версия каталога релизов не поддерживается.' }
  const revisions: GameDesignProfile[] = []; const keys = new Set<string>()
  for (const candidate of data.revisions) {
    const profile = validProfile(candidate, 'В каталоге релизов есть невалидная ревизия.')
    if ('ok' in profile) return profile
    const key = `${profile.id}@${profile.revision}`
    if (keys.has(key)) return { ok: false, message: 'В каталоге релизов есть повторная ревизия.' }
    keys.add(key); revisions.push(profile)
  }
  const releaseRef = data.releaseRef as ProfileRevisionRef
  if (!revisions.some((entry) => sameProfileRevision(profileRevisionRef(entry as ValidatedGameDesignProfile), releaseRef))) return { ok: false, message: 'Release reference отсутствует в каталоге.' }
  return { ok: true, manifest: { schemaVersion: 1, releaseRef: clone(releaseRef), revisions } }
}
export const RELEASE_MANIFEST = (() => { const parsed = loadReleaseManifest(); if (!parsed.ok) throw new Error(parsed.message); return parsed.manifest })()
const emptyCatalog = (): LocalProfileCatalog => ({ schemaVersion: 2, profiles: {}, selected: null })

export function validateLocalProfileCatalog(value: unknown): LocalProfileCatalogResult {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return { ok: false, message: 'Локальная история профилей повреждена.' }
  const data = value as Record<string, unknown>
  if (data.schemaVersion !== 2 || data.profiles === null || typeof data.profiles !== 'object' || Array.isArray(data.profiles)) return { ok: false, message: 'Версия локальной истории профилей не поддерживается.' }
  const profiles: Record<string, readonly GameDesignProfile[]> = {}
  for (const [id, candidates] of Object.entries(data.profiles as Record<string, unknown>)) {
    if (!Array.isArray(candidates) || id.length === 0) return { ok: false, message: 'История профиля повреждена.' }
    const revisions: GameDesignProfile[] = []; const numbers = new Set<number>()
    for (const candidate of candidates) {
      const profile = validProfile(candidate, 'В локальной истории есть невалидная ревизия.')
      if ('ok' in profile) return profile
      if (profile.source !== 'local' || profile.id !== id || numbers.has(profile.revision)) return { ok: false, message: 'Локальная история содержит чужую или повторную ревизию.' }
      numbers.add(profile.revision); revisions.push(profile)
    }
    profiles[id] = revisions
  }
  const selected = data.selected
  if (selected !== null && (selected === undefined || typeof selected !== 'object' || Array.isArray(selected))) return { ok: false, message: 'Ссылка на выбранную ревизию повреждена.' }
  return { ok: true, catalog: { schemaVersion: 2, profiles, selected: selected as ProfileRevisionRef | null } }
}
export function loadLocalProfileCatalog(storage: Storage | undefined = globalThis.localStorage): LocalProfileCatalogResult {
  const raw = storage?.getItem(GAME_DESIGN_LAB_STORAGE_KEY)
  if (raw === null || raw === undefined) return { ok: true, catalog: emptyCatalog() }
  try { return validateLocalProfileCatalog(JSON.parse(raw) as unknown) } catch { return { ok: false, message: 'Не удалось прочитать локальную историю профилей.' } }
}
export function allProfileRevisions(catalog: LocalProfileCatalog, manifest: ReleaseManifest = RELEASE_MANIFEST): readonly GameDesignProfile[] {
  const revisions = new Map(manifest.revisions.map((profile) => [`${profile.id}@${profile.revision}`, profile]))
  for (const profile of Object.values(catalog.profiles).flat()) {
    const key = `${profile.id}@${profile.revision}`
    if (!revisions.has(key)) revisions.set(key, profile)
  }
  return [...revisions.values()]
}
export function resolveSelectedProfile(catalog: LocalProfileCatalog, manifest: ReleaseManifest = RELEASE_MANIFEST): { ok: true; profile: ValidatedGameDesignProfile } | { ok: false; message: string } {
  const selected = catalog.selected ?? manifest.releaseRef
  const found = allProfileRevisions(catalog, manifest).find((profile) => sameProfileRevision(profileRevisionRef(profile as ValidatedGameDesignProfile), selected))
  if (!found) return { ok: false, message: 'Выбранная ревизия недоступна или повреждена.' }
  const validation = validateGameDesignProfile(found)
  if (!validation.ok || validation.profile.contentHash !== selected.contentHash) return { ok: false, message: 'Выбранная ревизия не прошла проверку content hash.' }
  return { ok: true, profile: validation.profile }
}
function writeCatalog(catalog: LocalProfileCatalog, storage: Storage | undefined) { storage?.setItem(GAME_DESIGN_LAB_STORAGE_KEY, JSON.stringify(catalog)) }
export function persistLocalProfileCatalog(catalog: LocalProfileCatalog, storage: Storage | undefined = globalThis.localStorage) { writeCatalog(catalog, storage) }
export function saveLocalRevision(base: ValidatedGameDesignProfile, draft: GameDesignProfile, storage: Storage | undefined = globalThis.localStorage): { ok: true; catalog: LocalProfileCatalog; profile: ValidatedGameDesignProfile } | { ok: false; message: string } {
  const current = loadLocalProfileCatalog(storage); if (!current.ok) return current
  const id = base.id
  const prior = current.catalog.profiles[id] ?? []; const nextRevision = Math.max(base.revision, ...prior.map(({ revision }) => revision)) + 1
  const local: GameDesignProfile = { ...clone(draft), id, name: base.name, revision: nextRevision, source: 'local', contentHash: '' }
  local.contentHash = computeProfileContentHash(local)
  const validation = validateGameDesignProfile(local)
  if (!validation.ok) return { ok: false, message: validation.errors.map(({ message }) => message).join(' ') }
  const profile = validation.profile
  const catalog: LocalProfileCatalog = { schemaVersion: 2, profiles: { ...current.catalog.profiles, [id]: [...prior, clone(profile) as GameDesignProfile] }, selected: profileRevisionRef(profile) }
  writeCatalog(catalog, storage); return { ok: true, catalog, profile }
}
export function createLocalProfile(base: ValidatedGameDesignProfile, name: string, storage: Storage | undefined = globalThis.localStorage): { ok: true; catalog: LocalProfileCatalog; profile: ValidatedGameDesignProfile } | { ok: false; message: string } {
  const trimmed = name.trim(); if (!trimmed) return { ok: false, message: 'Введите имя нового профиля.' }
  const current = loadLocalProfileCatalog(storage); if (!current.ok) return current
  if (Object.values(current.catalog.profiles).flat().some((profile) => profile.name === trimmed)) return { ok: false, message: 'Профиль с таким именем уже существует.' }
  const suffix = trimmed.toLocaleLowerCase('ru').replace(/[^a-zа-яё0-9]+/giu, '-').replace(/^-|-$/g, '') || 'profile'
  const id = `local:${suffix}`; if (current.catalog.profiles[id]) return { ok: false, message: 'Профиль с таким идентификатором уже существует.' }
  const local: GameDesignProfile = { ...(clone(base) as GameDesignProfile), id, name: trimmed, revision: 1, source: 'local', contentHash: '' }
  local.contentHash = computeProfileContentHash(local)
  const validation = validateGameDesignProfile(local)
  if (!validation.ok) return { ok: false, message: validation.errors.map(({ message }) => message).join(' ') }
  const profile = validation.profile
  const catalog: LocalProfileCatalog = { schemaVersion: 2, profiles: { ...current.catalog.profiles, [id]: [clone(profile) as GameDesignProfile] }, selected: profileRevisionRef(profile) }
  writeCatalog(catalog, storage); return { ok: true, catalog, profile }
}
export function selectLocalRevision(ref: ProfileRevisionRef | null, storage: Storage | undefined = globalThis.localStorage): LocalProfileCatalogResult {
  const current = loadLocalProfileCatalog(storage); if (!current.ok) return current
  const profile = resolveSelectedProfile({ ...current.catalog, selected: ref }); if (!profile.ok) return profile
  const catalog = { ...current.catalog, selected: ref }; writeCatalog(catalog, storage); return { ok: true, catalog }
}
