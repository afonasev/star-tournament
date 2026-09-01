import { canonicalSerialize, contentHash, isContentHash, type ContentHash } from '../core/canonical'

import { deepFreeze, type DeepReadonly } from './deepFreeze'

export const GAME_DESIGN_PROFILE_SCHEMA_VERSION = 1 as const

export type GameDesignProfileSource = 'shipped' | 'local'

export interface GameDesignRules {
  player: {
    startingHealth: number
  }
  weapons: {
    doubleBarrelShotgun: {
      startingAmmo: number
      damage: {
        head: number
        torso: number
        limb: number
      }
    }
  }
  match: {
    duration: {
      defaultMinutes: number
      minimumMinutes: number
      maximumMinutes: number
    }
  }
  scoring: {
    assist: {
      windowSeconds: number
      points: number
    }
    killChain: {
      maximumGapSeconds: number
      cumulativeTotals: [number, number, number, number, number]
      postFiveIncrementPoints: number
    }
  }
  death: {
    killcamSeconds: number
    corpseSeconds: number
  }
}

export interface GameDesignProfilePayload {
  schemaVersion: typeof GAME_DESIGN_PROFILE_SCHEMA_VERSION
  id: string
  name: string
  revision: number
  source: GameDesignProfileSource
  rules: GameDesignRules
}

export interface GameDesignProfile extends GameDesignProfilePayload {
  contentHash: string
}

export type ShippedGameDesignProfile = DeepReadonly<GameDesignProfile>
export type ValidatedGameDesignProfile = DeepReadonly<GameDesignProfile>

export type ProfileDescriptorGroup =
  | 'player'
  | 'shotgun'
  | 'match'
  | 'scoring-assist'
  | 'scoring-kill-chain'
  | 'death'

export type ProfileDescriptorUnit =
  | 'hp'
  | 'ammo'
  | 'damage'
  | 'minutes'
  | 'seconds'
  | 'points'

export interface NumericProfileDescriptor {
  path: string
  group: ProfileDescriptorGroup
  label: string
  description: string
  unit: ProfileDescriptorUnit
  minimum: number
  maximum: number
  step: number
}

const descriptors: NumericProfileDescriptor[] = [
  {
    path: 'rules.player.startingHealth',
    group: 'player',
    label: 'Стартовое здоровье',
    description: 'Запас здоровья игрока при старте жизни и после возрождения.',
    unit: 'hp',
    minimum: 1,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.startingAmmo',
    group: 'shotgun',
    label: 'Боезапас двухстволки',
    description: 'Количество выстрелов в начале каждой жизни.',
    unit: 'ammo',
    minimum: 0,
    maximum: 200,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.head',
    group: 'shotgun',
    label: 'Урон в голову',
    description: 'Суммарный урон полного попадания дроби в голову.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.torso',
    group: 'shotgun',
    label: 'Урон в корпус',
    description: 'Суммарный урон полного попадания дроби в корпус.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.limb',
    group: 'shotgun',
    label: 'Урон в конечности',
    description: 'Суммарный урон полного попадания дроби в руку или ногу.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.match.duration.defaultMinutes',
    group: 'match',
    label: 'Длительность матча по умолчанию',
    description: 'Предустановленная длительность нового матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.match.duration.minimumMinutes',
    group: 'match',
    label: 'Минимальная длительность матча',
    description: 'Нижняя граница длительности, доступная в настройках матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.match.duration.maximumMinutes',
    group: 'match',
    label: 'Максимальная длительность матча',
    description: 'Верхняя граница длительности, доступная в настройках матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.scoring.assist.windowSeconds',
    group: 'scoring-assist',
    label: 'Окно участия в убийстве',
    description: 'Максимальное время между нанесённым уроном и смертью цели для зачёта участия.',
    unit: 'seconds',
    minimum: 0,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.scoring.assist.points',
    group: 'scoring-assist',
    label: 'Очки за участие',
    description: 'Количество очков, начисляемое за одно участие в убийстве.',
    unit: 'points',
    minimum: 0,
    maximum: 1_000,
    step: 1,
  },
  {
    path: 'rules.scoring.killChain.maximumGapSeconds',
    group: 'scoring-kill-chain',
    label: 'Интервал серии убийств',
    description: 'Максимальный интервал между соседними убийствами одной серии.',
    unit: 'seconds',
    minimum: 0,
    maximum: 60,
    step: 0.1,
  },
  ...[1, 2, 3, 4, 5].map(
    (killCount): NumericProfileDescriptor => ({
      path: `rules.scoring.killChain.cumulativeTotals.${killCount - 1}`,
      group: 'scoring-kill-chain',
      label: `Сумма очков за серию из ${killCount}`,
      description: `Итоговая сумма очков за непрерывную серию из ${killCount} убийств.` ,
      unit: 'points',
      minimum: 0,
      maximum: 100_000,
      step: 1,
    }),
  ),
  {
    path: 'rules.scoring.killChain.postFiveIncrementPoints',
    group: 'scoring-kill-chain',
    label: 'Прирост после пятого убийства',
    description: 'Очки за каждое следующее убийство после пятого в той же серии.',
    unit: 'points',
    minimum: 0,
    maximum: 10_000,
    step: 1,
  },
  {
    path: 'rules.death.killcamSeconds',
    group: 'death',
    label: 'Длительность killcam',
    description: 'Время наблюдения после смерти до возрождения.',
    unit: 'seconds',
    minimum: 0,
    maximum: 15,
    step: 0.1,
  },
  {
    path: 'rules.death.corpseSeconds',
    group: 'death',
    label: 'Время существования тела',
    description: 'Время показа visual-only тела погибшей жизни.',
    unit: 'seconds',
    minimum: 0,
    maximum: 60,
    step: 0.1,
  },
]

export const GAME_DESIGN_PROFILE_DESCRIPTORS = deepFreeze(descriptors)

export type ProfileValidationErrorCode =
  | 'MISSING_FIELD'
  | 'INVALID_TYPE'
  | 'UNSUPPORTED_SCHEMA_VERSION'
  | 'INVALID_SOURCE'
  | 'UNRECOGNIZED_FIELD'
  | 'OUT_OF_RANGE'
  | 'INVALID_STEP'
  | 'MATCH_DURATION_ORDER'
  | 'KILL_CHAIN_TOTALS_NOT_INCREASING'
  | 'CONTENT_HASH_MISMATCH'

export interface ProfileValidationError {
  code: ProfileValidationErrorCode
  paths: readonly string[]
  message: string
}

export type ProfileValidationResult =
  | { ok: true; profile: ValidatedGameDesignProfile }
  | { ok: false; errors: readonly ProfileValidationError[] }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readPath(value: unknown, path: string): unknown {
  let cursor = value
  for (const segment of path.split('.')) {
    if (Array.isArray(cursor)) {
      const index = Number(segment)
      if (!Number.isInteger(index) || index < 0 || index >= cursor.length) return undefined
      cursor = cursor[index]
      continue
    }
    if (!isRecord(cursor) || !Object.hasOwn(cursor, segment)) return undefined
    cursor = cursor[segment]
  }
  return cursor
}

function matchesStep(value: number, minimum: number, step: number): boolean {
  const scaled = (value - minimum) / step
  return Math.abs(scaled - Math.round(scaled)) <= 1e-9
}

function collectLeafPaths(value: unknown, prefix: string, ancestors: Set<object>): string[] {
  if (typeof value !== 'object' || value === null) return [prefix]
  if (ancestors.has(value)) return [prefix]

  ancestors.add(value)
  try {
    if (Array.isArray(value)) {
      return value.length === 0
        ? [prefix]
        : value.flatMap((child, index) => collectLeafPaths(child, `${prefix}.${index}`, ancestors))
    }
    return Object.entries(value).flatMap(([key, child]) =>
      collectLeafPaths(child, `${prefix}.${key}`, ancestors),
    )
  } finally {
    ancestors.delete(value)
  }
}

export function profileContentPayload(profile: GameDesignProfile): GameDesignProfilePayload {
  const { contentHash: _contentHash, ...payload } = profile
  return payload
}

export function computeProfileContentHash(profile: GameDesignProfile): ContentHash {
  return contentHash(profileContentPayload(profile))
}

export function canonicalProfileJson(profile: GameDesignProfile): string {
  return canonicalSerialize(profile)
}

export function validateGameDesignProfile(value: unknown): ProfileValidationResult {
  const errors: ProfileValidationError[] = []
  if (!isRecord(value)) {
    return {
      ok: false,
      errors: [{ code: 'INVALID_TYPE', paths: ['$'], message: 'Профиль должен быть объектом.' }],
    }
  }

  const requiredStrings = ['id', 'name', 'contentHash'] as const
  for (const path of requiredStrings) {
    const field = readPath(value, path)
    if (field === undefined) {
      errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует обязательное поле ${path}.` })
    } else if (typeof field !== 'string' || field.length === 0) {
      errors.push({ code: 'INVALID_TYPE', paths: [path], message: `Поле ${path} должно быть непустой строкой.` })
    }
  }
  if (typeof value.contentHash === 'string' && value.contentHash.length > 0 && !isContentHash(value.contentHash)) {
    errors.push({
      code: 'INVALID_TYPE',
      paths: ['contentHash'],
      message: 'contentHash должен соответствовать формату fnv1a64-v1.',
    })
  }

  const allowedRootFields = new Set(['schemaVersion', 'id', 'name', 'revision', 'source', 'rules', 'contentHash'])
  for (const key of Object.keys(value).sort()) {
    if (!allowedRootFields.has(key)) {
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [key],
        message: `Поле ${key} не входит в schema GameDesignProfile.`,
      })
    }
  }

  const descriptorPathSet = new Set(GAME_DESIGN_PROFILE_DESCRIPTORS.map(({ path }) => path))
  for (const path of collectLeafPaths(value.rules, 'rules', new Set<object>()).sort()) {
    if (!descriptorPathSet.has(path)) {
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [path],
        message: `Поле ${path} не зарегистрировано в descriptor registry.`,
      })
    }
  }

  const schemaVersion = readPath(value, 'schemaVersion')
  if (schemaVersion === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['schemaVersion'], message: 'Отсутствует schemaVersion.' })
  } else if (schemaVersion !== GAME_DESIGN_PROFILE_SCHEMA_VERSION) {
    errors.push({
      code: 'UNSUPPORTED_SCHEMA_VERSION',
      paths: ['schemaVersion'],
      message: `Поддерживается schemaVersion ${GAME_DESIGN_PROFILE_SCHEMA_VERSION}.`,
    })
  }

  const revision = readPath(value, 'revision')
  if (revision === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['revision'], message: 'Отсутствует revision.' })
  } else if (typeof revision !== 'number' || !Number.isInteger(revision) || revision < 1) {
    errors.push({ code: 'INVALID_TYPE', paths: ['revision'], message: 'revision должен быть целым числом не меньше 1.' })
  }

  const source = readPath(value, 'source')
  if (source === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['source'], message: 'Отсутствует source.' })
  } else if (source !== 'shipped' && source !== 'local') {
    errors.push({ code: 'INVALID_SOURCE', paths: ['source'], message: 'source должен быть shipped или local.' })
  }

  for (const descriptor of GAME_DESIGN_PROFILE_DESCRIPTORS) {
    const field = readPath(value, descriptor.path)
    if (field === undefined) {
      errors.push({
        code: 'MISSING_FIELD',
        paths: [descriptor.path],
        message: `Отсутствует обязательное поле ${descriptor.path}.`,
      })
      continue
    }
    if (typeof field !== 'number' || !Number.isFinite(field)) {
      errors.push({
        code: 'INVALID_TYPE',
        paths: [descriptor.path],
        message: `Поле ${descriptor.path} должно быть конечным числом.`,
      })
      continue
    }
    if (field < descriptor.minimum || field > descriptor.maximum) {
      errors.push({
        code: 'OUT_OF_RANGE',
        paths: [descriptor.path],
        message: `${descriptor.path}: допустимый диапазон ${descriptor.minimum}–${descriptor.maximum} ${descriptor.unit}.`,
      })
    } else if (!matchesStep(field, descriptor.minimum, descriptor.step)) {
      errors.push({
        code: 'INVALID_STEP',
        paths: [descriptor.path],
        message: `${descriptor.path}: значение должно соответствовать шагу ${descriptor.step}.`,
      })
    }
  }

  const minimumMinutes = readPath(value, 'rules.match.duration.minimumMinutes')
  const defaultMinutes = readPath(value, 'rules.match.duration.defaultMinutes')
  const maximumMinutes = readPath(value, 'rules.match.duration.maximumMinutes')
  if (
    typeof minimumMinutes === 'number' &&
    Number.isFinite(minimumMinutes) &&
    typeof defaultMinutes === 'number' &&
    Number.isFinite(defaultMinutes) &&
    typeof maximumMinutes === 'number' &&
    Number.isFinite(maximumMinutes) &&
    !(minimumMinutes <= defaultMinutes && defaultMinutes <= maximumMinutes)
  ) {
    errors.push({
      code: 'MATCH_DURATION_ORDER',
      paths: [
        'rules.match.duration.minimumMinutes',
        'rules.match.duration.defaultMinutes',
        'rules.match.duration.maximumMinutes',
      ],
      message: 'Длительность матча должна удовлетворять minimum <= default <= maximum.',
    })
  }

  const totals = readPath(value, 'rules.scoring.killChain.cumulativeTotals')
  if (
    Array.isArray(totals) &&
    totals.length === 5 &&
    totals.every((total) => typeof total === 'number' && Number.isFinite(total)) &&
    totals.some((total, index) => index > 0 && total <= totals[index - 1]!)
  ) {
    errors.push({
      code: 'KILL_CHAIN_TOTALS_NOT_INCREASING',
      paths: [
        'rules.scoring.killChain.cumulativeTotals.0',
        'rules.scoring.killChain.cumulativeTotals.1',
        'rules.scoring.killChain.cumulativeTotals.2',
        'rules.scoring.killChain.cumulativeTotals.3',
        'rules.scoring.killChain.cumulativeTotals.4',
      ],
      message: 'Суммарные очки серии убийств должны строго возрастать.',
    })
  }

  if (errors.length === 0) {
    const profile = value as unknown as GameDesignProfile
    if (computeProfileContentHash(profile) !== profile.contentHash) {
      errors.push({
        code: 'CONTENT_HASH_MISMATCH',
        paths: ['contentHash'],
        message: 'contentHash не соответствует эффективному содержимому профиля.',
      })
    }
  }

  if (errors.length !== 0) {
    return { ok: false, errors: deepFreeze(errors) }
  }

  // Validation is the trust boundary: callers receive a detached immutable
  // snapshot, so later mutation of the untrusted input cannot stale its hash.
  const detached = JSON.parse(canonicalSerialize(value)) as GameDesignProfile
  return { ok: true, profile: deepFreeze(detached) }
}

const prototypePayload: GameDesignProfilePayload = {
  schemaVersion: GAME_DESIGN_PROFILE_SCHEMA_VERSION,
  id: 'prototype-v1',
  name: 'Prototype v1',
  revision: 1,
  source: 'shipped',
  rules: {
    player: { startingHealth: 100 },
    weapons: {
      doubleBarrelShotgun: {
        startingAmmo: 20,
        damage: { head: 100, torso: 70, limb: 30 },
      },
    },
    match: {
      duration: { defaultMinutes: 5, minimumMinutes: 1, maximumMinutes: 30 },
    },
    scoring: {
      assist: { windowSeconds: 5, points: 50 },
      killChain: {
        maximumGapSeconds: 10,
        cumulativeTotals: [100, 300, 500, 800, 1_200],
        postFiveIncrementPoints: 400,
      },
    },
    death: { killcamSeconds: 3, corpseSeconds: 10 },
  },
}

const prototypeWithHash: GameDesignProfile = {
  ...prototypePayload,
  contentHash: contentHash(prototypePayload),
}

const prototypeValidation = validateGameDesignProfile(prototypeWithHash)
if (!prototypeValidation.ok) {
  throw new Error(`Некорректный shipped profile: ${prototypeValidation.errors.map((error) => error.code).join(', ')}`)
}

export const PROTOTYPE_V1_PROFILE: ShippedGameDesignProfile = prototypeValidation.profile
