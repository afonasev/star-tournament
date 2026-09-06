import { useState, type ChangeEvent } from 'react'

import {
  GAME_DESIGN_PROFILE_DESCRIPTORS,
  allProfileRevisions,
  createLocalProfile,
  deleteLocalProfile,
  loadLocalProfileCatalog,
  RELEASE_MANIFEST,
  profileRevisionRef,
  saveLocalRevision,
  sameProfileRevision,
  selectLocalRevision,
  validateGameDesignProfile,
  type GameDesignProfile,
  type NumericProfileDescriptor,
  type ProfileDescriptorGroup,
  type ProfileRevisionRef,
  type ValidatedGameDesignProfile,
} from '../profiles'
import { persistProfileHistory, profileFilesystemAvailable, publishProfileRevision } from './profileRepository'

const GROUP_LABELS: Record<ProfileDescriptorGroup, string> = {
  bots: 'Боты', player: 'Игрок', 'player-capsule': 'Капсула игрока', 'player-movement': 'Движение', camera: 'Камера', shotgun: 'Дробовик', match: 'Матч', 'scoring-assist': 'Участия', 'scoring-kill-chain': 'Серии', death: 'Смерть и respawn', 'arena-generator-small': 'Генератор · маленькая', 'arena-generator-medium': 'Генератор · средняя', 'arena-generator-large': 'Генератор · большая',
}
const NAV_GROUPS: ReadonlyArray<ProfileDescriptorGroup> = [
  'player',
  'player-capsule',
  'player-movement',
  'camera',
  'shotgun',
  'match',
  'scoring-assist',
  'scoring-kill-chain',
  'death',
  'arena-generator-small',
  'arena-generator-medium',
  'arena-generator-large',
  'bots',
] as const

const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value)) as T
const getPath = (value: unknown, path: string): number => path.split('.').reduce<unknown>((current, key) => (current as Record<string, unknown>)[key], value) as number
const setPath = (value: GameDesignProfile, path: string, next: number): GameDesignProfile => {
  const draft = clone(value)
  const parts = path.split('.')
  let current = draft as unknown as Record<string, unknown>
  for (const part of parts.slice(0, -1)) current = current[part] as Record<string, unknown>
  current[parts.at(-1)!] = next
  return draft
}
const equal = (left: { readonly rules: unknown }, right: { readonly rules: unknown }) => JSON.stringify(left.rules) === JSON.stringify(right.rules)

export interface GameDesignLabProps {
  readonly selected: ValidatedGameDesignProfile
  readonly onSelected: (profile: ValidatedGameDesignProfile) => void
  readonly onClose: () => void
}

export function GameDesignLab({ selected, onSelected, onClose }: GameDesignLabProps) {
  const [base, setBase] = useState<ValidatedGameDesignProfile>(selected)
  const [draft, setDraft] = useState<GameDesignProfile>(() => clone(selected) as GameDesignProfile)
  const [message, setMessage] = useState<string | null>(null)
  const [confirmClose, setConfirmClose] = useState(false)
  const [newProfileName, setNewProfileName] = useState('')
  const [publishing, setPublishing] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [releaseRef, setReleaseRef] = useState<ProfileRevisionRef>(RELEASE_MANIFEST.releaseRef)
  const catalogResult = loadLocalProfileCatalog()
  const revisions = catalogResult.ok ? allProfileRevisions(catalogResult.catalog) : [selected]
  const profileIds = [...new Set(revisions.map((profile) => profile.id))]
  const selectedProfileId = profileIds.includes(base.id) ? base.id : profileIds[0]!
  const visibleRevisions = revisions.filter((profile) => profile.id === selectedProfileId)
  const isRelease = sameProfileRevision(profileRevisionRef(base), releaseRef)
  const canDeleteProfile = catalogResult.ok && Boolean(catalogResult.catalog.profiles[selectedProfileId]) && selectedProfileId !== releaseRef.id
  const dirty = !equal(base, draft)
  const changes = GAME_DESIGN_PROFILE_DESCRIPTORS.filter((descriptor) => getPath(base, descriptor.path) !== getPath(draft, descriptor.path))
  const [selectedSection, setSelectedSection] = useState<ProfileDescriptorGroup>(NAV_GROUPS[0])
  const selectedFields = GAME_DESIGN_PROFILE_DESCRIPTORS.filter((descriptor) => descriptor.group === selectedSection)
  const changeCounts = Object.fromEntries(NAV_GROUPS.map((group) => [group, 0])) as Record<
    ProfileDescriptorGroup,
    number
  >
  for (const descriptor of GAME_DESIGN_PROFILE_DESCRIPTORS) {
    if (getPath(base, descriptor.path) !== getPath(draft, descriptor.path)) {
      changeCounts[descriptor.group] += 1
    }
  }
  const choose = (event: ChangeEvent<HTMLSelectElement>) => {
    const profile = revisions.find((entry) => `${entry.id}@${entry.revision}` === event.target.value)
    if (!profile) return
    const result = selectLocalRevision(profileRevisionRef(profile))
    if (!result.ok) { setMessage(result.message); return }
    const validated = validateGameDesignProfile(profile)
    if (!validated.ok) { setMessage(validated.errors[0]?.message ?? 'Невалидная ревизия.'); return }
    void persistProfileHistory(result.catalog).catch((cause) => setMessage(cause instanceof Error ? cause.message : String(cause)))
    setBase(validated.profile); setDraft(clone(validated.profile) as GameDesignProfile); onSelected(validated.profile); setMessage(null)
  }
  const chooseProfile = (event: ChangeEvent<HTMLSelectElement>) => {
    const latest = revisions.filter((profile) => profile.id === event.target.value).at(-1)
    if (!latest) return
    choose({ target: { value: `${latest.id}@${latest.revision}` } } as ChangeEvent<HTMLSelectElement>)
  }
  const save = () => {
    const candidate = { ...draft, contentHash: '' }
    // The repository derives the final immutable identity/hash before validation.
    const result = saveLocalRevision(base, candidate)
    if (!result.ok) { setMessage(result.message); return }
    void persistProfileHistory(result.catalog).catch((cause) => setMessage(cause instanceof Error ? cause.message : String(cause)))
    setBase(result.profile); setDraft(clone(result.profile) as GameDesignProfile); onSelected(result.profile); setMessage(`Сохранена ${result.profile.id} v${result.profile.revision}.`)
  }
  const createProfile = () => {
    const result = createLocalProfile(base, newProfileName)
    if (!result.ok) { setMessage(result.message); return }
    void persistProfileHistory(result.catalog).catch((cause) => setMessage(cause instanceof Error ? cause.message : String(cause)))
    setNewProfileName(''); setBase(result.profile); setDraft(clone(result.profile) as GameDesignProfile); onSelected(result.profile); setMessage(`Создан профиль ${result.profile.name} · v1.`)
  }
  const publish = async () => {
    setPublishing(true); setMessage(null)
    try {
      const manifest = await publishProfileRevision(base)
      setReleaseRef(manifest.releaseRef)
      setMessage(`Релизная версия сохранена в balance/releases.json: ${base.id} v${base.revision}.`)
    } catch (cause) { setMessage(cause instanceof Error ? cause.message : String(cause)) }
    finally { setPublishing(false) }
  }
  const deleteProfile = () => {
    const result = deleteLocalProfile(selectedProfileId, releaseRef)
    if (!result.ok) { setMessage(result.message); return }
    void persistProfileHistory(result.catalog).catch((cause) => setMessage(cause instanceof Error ? cause.message : String(cause)))
    const fallback = revisions.find((profile) => sameProfileRevision(profileRevisionRef(profile as ValidatedGameDesignProfile), releaseRef))
    if (!fallback) { setMessage('Не найдена релизная ревизия после удаления профиля.'); return }
    const validation = validateGameDesignProfile(fallback)
    if (!validation.ok) { setMessage(validation.errors[0]?.message ?? 'Невалидная релизная ревизия.'); return }
    setConfirmDelete(false); setBase(validation.profile); setDraft(clone(validation.profile) as GameDesignProfile); onSelected(validation.profile); setMessage('Нерелизный профиль удалён целиком.')
  }
  const requestClose = () => { if (dirty) setConfirmClose(true); else onClose() }
  const pickSection = (group: ProfileDescriptorGroup) => {
    setSelectedSection(group)
  }
  return <main className="game-design-lab" aria-labelledby="game-design-lab-title">
    <header className="game-design-lab__header">
      <div>
        <p>STAR TOURNAMENT · GAME DESIGN LAB</p>
        <h1 id="game-design-lab-title">Лаборатория геймдизайна</h1>
        <output aria-label="Выбранная ревизия">{base.id} · v{base.revision} · {base.contentHash}{isRelease ? ' · РЕЛИЗНАЯ' : ''}</output>
      </div>
      <button type="button" onClick={requestClose}>В главное меню</button>
    </header>
    <section className="game-design-lab__toolbar" aria-label="Профили и ревизии">
      <label>Профиль
        <select aria-label="Профиль" value={selectedProfileId} onChange={chooseProfile}>
          {profileIds.map((id) => {
            const profile = revisions.find((entry) => entry.id === id)
            const containsRelease = revisions.some((entry) => entry.id === id && sameProfileRevision(profileRevisionRef(entry as ValidatedGameDesignProfile), releaseRef))
            return <option key={id} value={id}>{profile?.name ?? id}{containsRelease ? ' · РЕЛИЗ' : ''}</option>
          })}
        </select>
      </label>
      <label>Ревизия
        <select aria-label="Ревизия профиля" value={`${base.id}@${base.revision}`} onChange={choose}>
          {visibleRevisions.map((profile) => (
            <option key={`${profile.id}@${profile.revision}`} value={`${profile.id}@${profile.revision}`}>
              {profile.name} · v{profile.revision}{sameProfileRevision(profileRevisionRef(profile as ValidatedGameDesignProfile), releaseRef) ? ' · РЕЛИЗНАЯ' : profile.source === 'shipped' ? ' · baseline' : ''}
            </option>
          ))}
        </select>
      </label>
      <button type="button" disabled={!dirty} onClick={save}>
        Сохранить новую ревизию
      </button>
      <label>Новый профиль
        <input aria-label="Имя нового профиля" value={newProfileName} onChange={(event) => setNewProfileName(event.target.value)} />
      </label>
      <button type="button" onClick={createProfile}>Создать профиль</button>
      <button type="button" disabled={!canDeleteProfile} title={canDeleteProfile ? 'Удалить весь нерелизный профиль и все его ревизии' : 'Релизный или поставляемый профиль удалить нельзя'} onClick={() => setConfirmDelete(true)}>Удалить профиль</button>
      <button type="button" disabled={!profileFilesystemAvailable || publishing || isRelease} title={!profileFilesystemAvailable ? 'Публикация доступна на dev-стенде' : isRelease ? 'Эта версия уже опубликована в репозитории' : 'Сохранить точный snapshot в репозитории'} onClick={() => void publish()}>
        {publishing ? 'Запись релиза…' : isRelease ? 'Релизная версия' : 'Сделать релизной версией'}
      </button>
      {message && <p role="status">{message}</p>}
    </section>
    <div className="game-design-lab__content">
      <aside className="game-design-lab__section-nav" aria-label="Переход по разделам">
        {NAV_GROUPS.map((group) => (
          <button
            key={group}
            type="button"
            data-active={selectedSection === group}
            data-dirty={changeCounts[group] > 0 ? 'true' : undefined}
            onClick={() => pickSection(group)}
          >
            <span className="game-design-lab__section-label">{GROUP_LABELS[group]}</span>
            {changeCounts[group] > 0 ? <span className="game-design-lab__section-badge">{changeCounts[group]}</span> : null}
          </button>
        ))}
      </aside>
      <section className="game-design-lab__fields" aria-label="Параметры профиля">
        <fieldset key={selectedSection}>
          <legend>{GROUP_LABELS[selectedSection]}</legend>
          {selectedFields.map((descriptor) => (
            <Field
              key={descriptor.path}
              descriptor={descriptor}
              value={getPath(draft, descriptor.path)}
              onChange={(next) => {
                setDraft((current) => setPath(current, descriptor.path, next))
                setMessage(null)
              }}
            />
          ))}
        </fieldset>
      </section>
      <aside className="game-design-lab__changes" aria-label="Changelog">
        <h2>Changelog</h2>
        <p>Изменения относительно выбранной ревизии.</p>
        {changes.length === 0 ? <p>Нет изменений.</p> : <ol>{changes.map((descriptor) => {
          const before = getPath(base, descriptor.path)
          const after = getPath(draft, descriptor.path)
          const delta = after - before
          return <li key={descriptor.path}><strong>{descriptor.label}</strong><span>{before} → {after} ({delta > 0 ? '+' : ''}{delta} {descriptor.unit})</span></li>
        })}</ol>}
        <p>Текущий матч не меняется: ревизия применяется только при следующем запуске.</p>
      </aside>
    </div>
    {confirmClose && <div className="game-design-lab__confirm" role="dialog" aria-modal="true" aria-label="Несохранённые изменения"><div><h2>Отбросить несохранённые изменения?</h2><p>Черновик не станет конфигурацией матча без сохранения.</p><button type="button" onClick={() => setConfirmClose(false)}>Продолжить редактирование</button><button type="button" onClick={onClose}>Отбросить</button></div></div>}
    {confirmDelete && <div className="game-design-lab__confirm" role="dialog" aria-modal="true" aria-label="Удаление профиля"><div><h2>Удалить профиль целиком?</h2><p>Будут удалены все локальные ревизии этого профиля. Отдельные ревизии удалить нельзя.</p><button type="button" onClick={() => setConfirmDelete(false)}>Отмена</button><button type="button" onClick={deleteProfile}>Удалить профиль</button></div></div>}
  </main>
}

function Field({ descriptor, value, onChange }: { readonly descriptor: Readonly<NumericProfileDescriptor>; readonly value: number; readonly onChange: (value: number) => void }) {
  return <label><span>{descriptor.label} · {descriptor.unit}</span><small>{descriptor.description} Диапазон: {descriptor.minimum}–{descriptor.maximum}, шаг {descriptor.step}.</small><input aria-label={descriptor.label} type="number" min={descriptor.minimum} max={descriptor.maximum} step={descriptor.step} value={value} onChange={(event) => onChange(Number(event.target.value))} /></label>
}
