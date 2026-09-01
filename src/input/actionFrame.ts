import { canonicalSerialize, parseCanonicalJson } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const ACTION_FRAME_SCHEMA_VERSION = 1 as const

export interface NormalizedAction {
  readonly participantId: string
  readonly actionId: string
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

function compareActions(first: NormalizedAction, second: NormalizedAction): number {
  const participantOrder = first.participantId < second.participantId
    ? -1
    : first.participantId > second.participantId
      ? 1
      : 0
  if (participantOrder !== 0) {
    return participantOrder
  }
  return first.actionId < second.actionId ? -1 : first.actionId > second.actionId ? 1 : 0
}

export function createActionFrame(tick: number, actions: readonly NormalizedAction[]): ActionFrame {
  if (!Number.isSafeInteger(tick) || tick < 1) {
    throw new RangeError('ActionFrame.tick must be a positive safe integer')
  }

  const normalized = actions.map((action, index) => {
    const participantId = expectNonEmptyString(action.participantId, `ActionFrame.actions[${index}].participantId`)
    const actionId = expectNonEmptyString(action.actionId, `ActionFrame.actions[${index}].actionId`)
    if (!Number.isFinite(action.value)) {
      throw new TypeError(`ActionFrame.actions[${index}].value must be finite`)
    }
    return { participantId, actionId, value: action.value }
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
      actionId: expectNonEmptyString(actionRecord.actionId, `ActionFrame.actions[${index}].actionId`),
      value: actionRecord.value as number,
    }
  })

  return createActionFrame(record.tick as number, actions)
}

export function serializeActionFrame(frame: ActionFrame): string {
  return canonicalSerialize(parseActionFrame(frame))
}
