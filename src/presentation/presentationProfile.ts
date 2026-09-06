import {
  canonicalSerialize,
  contentHash,
  isContentHash,
  type ContentHash,
} from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const PRESENTATION_PROFILE_SCHEMA_VERSION = 1 as const

export type PresentationProfileSource = 'shipped' | 'local'

export interface PresentationProfilePayload {
  schemaVersion: typeof PRESENTATION_PROFILE_SCHEMA_VERSION
  id: string
  name: string
  revision: number
  source: PresentationProfileSource
  render: {
    backingBufferPixelBudget: number
    windowOpacity: number
    barrierOpacity: number
    localLightIntensity: number
    localLightDistance: number
    localLightCount: number
    studioFillIntensity: number
    studioKeyIntensity: number
    localLightShadowCount: number
    localLightShadowMapSize: number
    localLightShadowRadius: number
    reliefDepth: number
    materialRepeatScale: number
  }
  ui: {
    maximumPublicationsPerSecond: number
  }
  death: {
    corpseSlideDistanceMeters: number
  }
}

export interface PresentationProfile extends PresentationProfilePayload {
  contentHash: string
}

export type ShippedPresentationProfile = Readonly<PresentationProfile>
export type ValidatedPresentationProfile = Readonly<PresentationProfile>

export type PresentationProfileDescriptorGroup = 'render' | 'ui' | 'death'
export type PresentationProfileDescriptorUnit = 'pixels' | 'publications-per-second' | 'ratio' | 'meters' | 'count' | 'intensity'

export interface NumericPresentationProfileDescriptor {
  path: string
  group: PresentationProfileDescriptorGroup
  label: string
  description: string
  unit: PresentationProfileDescriptorUnit
  minimum: number
  maximum: number
  step: number
}

const descriptors: NumericPresentationProfileDescriptor[] = [
  { path:'death.corpseSlideDistanceMeters',group:'death',label:'Сдвиг тела после смерти',description:'Горизонтальная renderer-only дистанция, на которую тело смещается от смертельного выстрела.',unit:'meters',minimum:0,maximum:1.5,step:0.05 },
  {path:'render.materialRepeatScale',group:'render',label:'Ритм панелей',description:'Частота повторения фактуры по semantic sector.',unit:'ratio',minimum:0.5,maximum:4,step:0.5},
  { path:'render.windowOpacity',group:'render',label:'Прозрачность окна',description:'Доля непрозрачности непростреливаемого стекла.',unit:'ratio',minimum:0.1,maximum:0.8,step:0.05 },
  { path:'render.barrierOpacity',group:'render',label:'Прозрачность барьера',description:'Видимость movement-only энергетической сетки.',unit:'ratio',minimum:0.1,maximum:0.8,step:0.05 },
  { path:'render.localLightIntensity',group:'render',label:'Сила локального света',description:'Световой акцент у маршрутной лампы.',unit:'intensity',minimum:0,maximum:20,step:0.5 },
  { path:'render.localLightDistance',group:'render',label:'Дальность лампы',description:'Расстояние затухания локального света.',unit:'meters',minimum:2,maximum:20,step:1 },
  { path:'render.localLightCount',group:'render',label:'Бюджет ламп',description:'Верхний предел локальных источников на High/Ultra; Balanced использует половину, Low ноль.',unit:'count',minimum:0,maximum:24,step:2 },
  { path:'render.studioFillIntensity',group:'render',label:'Заполняющий свет арены',description:'Яркость общего света до локальных ламп.',unit:'intensity',minimum:0.2,maximum:2.4,step:0.1 },
  { path:'render.studioKeyIntensity',group:'render',label:'Направленный свет арены',description:'Яркость мягкого направленного света architectural shell.',unit:'intensity',minimum:0.2,maximum:2.4,step:0.1 },
  { path:'render.localLightShadowCount',group:'render',label:'Бюджет теней ламп',description:'Верхний предел мягких теней от локальных ламп на Ultra.',unit:'count',minimum:0,maximum:4,step:1 },
  { path:'render.localLightShadowMapSize',group:'render',label:'Разрешение тени лампы',description:'Размер одной стороны cube shadow map локальной лампы.',unit:'pixels',minimum:256,maximum:1024,step:256 },
  { path:'render.localLightShadowRadius',group:'render',label:'Мягкость тени лампы',description:'Радиус PCF-смягчения локальной тени: меньший радиус даёт более чёткий контакт.',unit:'ratio',minimum:0.5,maximum:8,step:0.5 },
  { path:'render.reliefDepth',group:'render',label:'Глубина рельефа',description:'Толщина декоративной рамки неглубокой ниши.',unit:'meters',minimum:0.02,maximum:0.2,step:0.02 },

  {
    path: 'render.backingBufferPixelBudget',
    group: 'render',
    label: 'Бюджет WebGL backing buffer',
    description: 'Максимальное количество пикселей backing buffer для всего игрового canvas.',
    unit: 'pixels',
    minimum: 230_400,
    maximum: 8_294_400,
    step: 57_600,
  },
  {
    path: 'ui.maximumPublicationsPerSecond',
    group: 'ui',
    label: 'Максимальная частота публикации HUD',
    description: 'Верхняя граница периодических публикаций gameplay-состояния в DOM HUD.',
    unit: 'publications-per-second',
    minimum: 1,
    maximum: 60,
    step: 1,
  },
]

export const PRESENTATION_PROFILE_DESCRIPTORS = deepFreeze(descriptors)

export type PresentationProfileValidationErrorCode =
  | 'MISSING_FIELD'
  | 'INVALID_TYPE'
  | 'UNSUPPORTED_SCHEMA_VERSION'
  | 'INVALID_SOURCE'
  | 'UNRECOGNIZED_FIELD'
  | 'OUT_OF_RANGE'
  | 'INVALID_STEP'
  | 'CONTENT_HASH_MISMATCH'

export interface PresentationProfileValidationError {
  code: PresentationProfileValidationErrorCode
  paths: readonly string[]
  message: string
}

export type PresentationProfileValidationResult =
  | { ok: true; profile: ValidatedPresentationProfile }
  | { ok: false; errors: readonly PresentationProfileValidationError[] }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readPath(value: unknown, path: string): unknown {
  let cursor = value
  for (const segment of path.split('.')) {
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

export function presentationProfileContentPayload(
  profile: PresentationProfile,
): PresentationProfilePayload {
  const { contentHash: _contentHash, ...payload } = profile
  return payload
}

export function computePresentationProfileContentHash(
  profile: PresentationProfile,
): ContentHash {
  return contentHash(presentationProfileContentPayload(profile))
}

export function canonicalPresentationProfileJson(profile: PresentationProfile): string {
  return canonicalSerialize(profile)
}

export function validatePresentationProfile(value: unknown): PresentationProfileValidationResult {
  const errors: PresentationProfileValidationError[] = []
  if (!isRecord(value)) {
    return {
      ok: false,
      errors: [{ code: 'INVALID_TYPE', paths: ['$'], message: 'Presentation profile должен быть объектом.' }],
    }
  }

  for (const path of ['id', 'name', 'contentHash'] as const) {
    const field = readPath(value, path)
    if (field === undefined) {
      errors.push({
        code: 'MISSING_FIELD',
        paths: [path],
        message: `Отсутствует обязательное поле ${path}.`,
      })
    } else if (typeof field !== 'string' || field.length === 0) {
      errors.push({
        code: 'INVALID_TYPE',
        paths: [path],
        message: `Поле ${path} должно быть непустой строкой.`,
      })
    }
  }
  if (typeof value.contentHash === 'string' && value.contentHash.length > 0 && !isContentHash(value.contentHash)) {
    errors.push({
      code: 'INVALID_TYPE',
      paths: ['contentHash'],
      message: 'contentHash должен соответствовать формату fnv1a64-v1.',
    })
  }

  const allowedRootFields = new Set([
    'schemaVersion',
    'id',
    'name',
    'revision',
    'source',
    'render',
    'ui',
    'death',
    'contentHash',
  ])
  for (const key of Object.keys(value).sort()) {
    if (!allowedRootFields.has(key)) {
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [key],
        message: `Поле ${key} не входит в schema PresentationProfile.`,
      })
    }
  }

  const descriptorPathSet = new Set(PRESENTATION_PROFILE_DESCRIPTORS.map(({ path }) => path))
  for (const root of ['render', 'ui', 'death'] as const) {
    for (const path of collectLeafPaths(value[root], root, new Set<object>()).sort()) {
      if (!descriptorPathSet.has(path)) {
        errors.push({
          code: 'UNRECOGNIZED_FIELD',
          paths: [path],
          message: `Поле ${path} не зарегистрировано в descriptor registry.`,
        })
      }
    }
  }

  const schemaVersion = readPath(value, 'schemaVersion')
  if (schemaVersion === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['schemaVersion'], message: 'Отсутствует schemaVersion.' })
  } else if (schemaVersion !== PRESENTATION_PROFILE_SCHEMA_VERSION) {
    errors.push({
      code: 'UNSUPPORTED_SCHEMA_VERSION',
      paths: ['schemaVersion'],
      message: `Поддерживается schemaVersion ${PRESENTATION_PROFILE_SCHEMA_VERSION}.`,
    })
  }

  const revision = readPath(value, 'revision')
  if (revision === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['revision'], message: 'Отсутствует revision.' })
  } else if (typeof revision !== 'number' || !Number.isInteger(revision) || revision < 1) {
    errors.push({
      code: 'INVALID_TYPE',
      paths: ['revision'],
      message: 'revision должен быть целым числом не меньше 1.',
    })
  }

  const source = readPath(value, 'source')
  if (source === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['source'], message: 'Отсутствует source.' })
  } else if (source !== 'shipped' && source !== 'local') {
    errors.push({
      code: 'INVALID_SOURCE',
      paths: ['source'],
      message: 'source должен быть shipped или local.',
    })
  }

  for (const descriptor of PRESENTATION_PROFILE_DESCRIPTORS) {
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

  if (errors.length === 0) {
    const profile = value as unknown as PresentationProfile
    if (computePresentationProfileContentHash(profile) !== profile.contentHash) {
      errors.push({
        code: 'CONTENT_HASH_MISMATCH',
        paths: ['contentHash'],
        message: 'contentHash не соответствует эффективному содержимому presentation profile.',
      })
    }
  }

  if (errors.length !== 0) return { ok: false, errors: deepFreeze(errors) }

  const detached = JSON.parse(canonicalSerialize(value)) as PresentationProfile
  return { ok: true, profile: deepFreeze(detached) }
}

const balancedPayload: PresentationProfilePayload = {
  schemaVersion: PRESENTATION_PROFILE_SCHEMA_VERSION,
  id: 'presentation-balanced-v1',
  name: 'Presentation Balanced v1',
  revision: 6,
  source: 'shipped',
  render: { backingBufferPixelBudget: 2_073_600, windowOpacity:0.25, barrierOpacity:0.2,localLightIntensity:8,localLightDistance:8,localLightCount:16,studioFillIntensity:0.4,studioKeyIntensity:0.5,localLightShadowCount:4,localLightShadowMapSize:512,localLightShadowRadius:0.5,reliefDepth:0.08,materialRepeatScale:1.5 },
  ui: { maximumPublicationsPerSecond: 10 },
  death: { corpseSlideDistanceMeters: 0.65 },
}

const balancedWithHash: PresentationProfile = {
  ...balancedPayload,
  contentHash: contentHash(balancedPayload),
}

const balancedValidation = validatePresentationProfile(balancedWithHash)
if (!balancedValidation.ok) {
  throw new Error(
    `Некорректный shipped presentation profile: ${balancedValidation.errors.map((error) => error.code).join(', ')}`,
  )
}

export const PRESENTATION_BALANCED_V1_PROFILE: ShippedPresentationProfile =
  balancedValidation.profile
