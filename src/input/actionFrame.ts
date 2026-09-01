import {
  canonicalSerialize,
  contentHash,
  parseCanonicalJson,
  type ContentHash,
} from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const ACTION_FRAME_SCHEMA_VERSION = 2 as const

export const CANONICAL_ACTION_IDS = deepFreeze([
  'move-forward',
  'move-right',
  'look-yaw',
  'look-pitch',
  'jump',
  'fire',
] as const)

export type CanonicalActionId = (typeof CANONICAL_ACTION_IDS)[number]
export type CanonicalActionValueKind = 'axis' | 'pressed'

export interface CanonicalActionDescriptor {
  readonly id: CanonicalActionId
  readonly minimum: number
  readonly maximum: number
  readonly valueKind: CanonicalActionValueKind
}

export const CANONICAL_ACTION_DESCRIPTORS: readonly CanonicalActionDescriptor[] = deepFreeze([
  { id: 'move-forward', minimum: -1, maximum: 1, valueKind: 'axis' },
  { id: 'move-right', minimum: -1, maximum: 1, valueKind: 'axis' },
  { id: 'look-yaw', minimum: -1, maximum: 1, valueKind: 'axis' },
  { id: 'look-pitch', minimum: -1, maximum: 1, valueKind: 'axis' },
  { id: 'jump', minimum: 0, maximum: 1, valueKind: 'pressed' },
  { id: 'fire', minimum: 0, maximum: 1, valueKind: 'pressed' },
])

const actionDescriptorById = new Map(
  CANONICAL_ACTION_DESCRIPTORS.map((descriptor) => [descriptor.id, descriptor]),
)
const actionOrderById = new Map(
  CANONICAL_ACTION_IDS.map((actionId, index) => [actionId, index]),
)

export interface NormalizedAction {
  readonly participantId: string
  readonly actionId: CanonicalActionId
  readonly value: number
}

export interface ActionFrame {
  readonly schemaVersion: typeof ACTION_FRAME_SCHEMA_VERSION
  readonly tick: number
  readonly actions: readonly NormalizedAction[]
}

function expectRecord(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${path} must be an object`)
  }
  return value as Record<string, unknown>
}

function expectExactKeys(record: Record<string, unknown>, keys: readonly string[], path: string): void {
  const expected = [...keys].sort()
  const actual = Object.keys(record).sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    throw new TypeError(`${path} must contain exactly: ${expected.join(', ')}`)
  }
}

function expectNonEmptyString(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new TypeError(`${path} must be a non-empty string`)
  }
  return value
}

function expectCanonicalActionId(value: unknown, path: string): CanonicalActionId {
  const actionId = expectNonEmptyString(value, path)
  if (!actionDescriptorById.has(actionId as CanonicalActionId)) {
    throw new TypeError(`${path} must be one of: ${CANONICAL_ACTION_IDS.join(', ')}`)
  }
  return actionId as CanonicalActionId
}

function normalizeActionValue(actionId: CanonicalActionId, value: unknown, path: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new TypeError(`${path} must be finite`)
  }

  const descriptor = actionDescriptorById.get(actionId)
  if (descriptor === undefined) {
    throw new TypeError(`Unknown canonical action: ${actionId}`)
  }
  if (value < descriptor.minimum || value > descriptor.maximum) {
    throw new RangeError(
      `${path} for ${actionId} must be between ${descriptor.minimum} and ${descriptor.maximum}`,
    )
  }
  if (descriptor.valueKind === 'pressed' && value !== 0 && value !== 1) {
    throw new RangeError(`${path} for ${actionId} must be the pressed state 0 or 1`)
  }
  return Object.is(value, -0) ? 0 : value
}

function compareActions(first: NormalizedAction, second: NormalizedAction): number {
  const participantOrder = first.participantId < second.participantId
    ? -1
    : first.participantId > second.participantId
      ? 1
      : 0
  if (participantOrder !== 0) {
    return participantOrder
  }
  return actionOrderById.get(first.actionId)! - actionOrderById.get(second.actionId)!
}

export function createActionFrame(tick: number, actions: readonly NormalizedAction[]): ActionFrame {
  if (!Number.isSafeInteger(tick) || tick < 1) {
    throw new RangeError('ActionFrame.tick must be a positive safe integer')
  }

  const normalized = actions.map((action, index) => {
    const participantId = expectNonEmptyString(action.participantId, `ActionFrame.actions[${index}].participantId`)
    const actionId = expectCanonicalActionId(action.actionId, `ActionFrame.actions[${index}].actionId`)
    const value = normalizeActionValue(
      actionId,
      action.value,
      `ActionFrame.actions[${index}].value`,
    )
    return { participantId, actionId, value }
  }).sort(compareActions)

  for (let index = 1; index < normalized.length; index += 1) {
    const previous = normalized[index - 1]
    const current = normalized[index]
    if (
      previous !== undefined
      && current !== undefined
      && previous.participantId === current.participantId
      && previous.actionId === current.actionId
    ) {
      throw new TypeError(
        `ActionFrame contains duplicate action key ${current.participantId}/${current.actionId}`,
      )
    }
  }

  return deepFreeze({
    schemaVersion: ACTION_FRAME_SCHEMA_VERSION,
    tick,
    actions: normalized,
  })
}

export function parseActionFrame(input: string | unknown): ActionFrame {
  const value = typeof input === 'string' ? parseCanonicalJson(input) : input
  const record = expectRecord(value, 'ActionFrame')
  expectExactKeys(record, ['schemaVersion', 'tick', 'actions'], 'ActionFrame')
  if (record.schemaVersion !== ACTION_FRAME_SCHEMA_VERSION) {
    throw new TypeError(`Unsupported ActionFrame.schemaVersion: ${String(record.schemaVersion)}`)
  }
  if (!Array.isArray(record.actions)) {
    throw new TypeError('ActionFrame.actions must be an array')
  }

  const actions = record.actions.map((action, index): NormalizedAction => {
    const actionRecord = expectRecord(action, `ActionFrame.actions[${index}]`)
    expectExactKeys(
      actionRecord,
      ['participantId', 'actionId', 'value'],
      `ActionFrame.actions[${index}]`,
    )
    return {
      participantId: expectNonEmptyString(
        actionRecord.participantId,
        `ActionFrame.actions[${index}].participantId`,
      ),
      actionId: expectCanonicalActionId(
        actionRecord.actionId,
        `ActionFrame.actions[${index}].actionId`,
      ),
      value: actionRecord.value as number,
    }
  })

  return createActionFrame(record.tick as number, actions)
}

export function serializeActionFrame(frame: ActionFrame): string {
  return canonicalSerialize(parseActionFrame(frame))
}

export function computeActionFrameHash(frame: ActionFrame): ContentHash {
  return contentHash(parseActionFrame(frame))
}
