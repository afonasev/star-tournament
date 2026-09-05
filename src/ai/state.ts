import type { CollisionVector3 } from '../collision/contracts'
import { createSeededRngState, parseSeededRngState, type SeededRngState } from '../simulation/rng'

export const BOT_AI_VERSION = 'bot-ai-v1' as const
export interface Sighting { id: string; lifeId: string; position: CollisionVector3; tick: number }
export interface BotState {
  version: typeof BOT_AI_VERSION
  rng: SeededRngState
  memory: Sighting[]
  intent: string
  goal: CollisionVector3
  targetId: string
  lastHealth: number
  alertTick: number
  alertYaw: number
  noticedTick: number
  decisionTick: number
  strafeTick: number
  strafeSign: number
  jumpTick: number
  retreatTick: number
  supportTick: number
  recoveryTick: number
  progressTick: number
  progressPosition: CollisionVector3
  route: CollisionVector3[]
  personality: number
}
export function createBotState(seed: number, id: string, position: CollisionVector3): BotState {
  // FNV mixing is a seed-derivation invariant, not a balance parameter.
  let mixed = seed >>> 0
  for (const char of id) mixed = Math.imul(mixed ^ char.charCodeAt(0), 16777619) >>> 0
  return { version: BOT_AI_VERSION, rng: createSeededRngState(mixed), memory: [], intent: 'search', goal: position, targetId: '', lastHealth: 0, alertTick: 0, alertYaw: 0, noticedTick: 0, decisionTick: 0, strafeTick: 0, strafeSign: 1, jumpTick: 0, retreatTick: 0, supportTick: 0, recoveryTick: 0, progressTick: 0, progressPosition: position, route: [], personality: mixed / 0xffffffff }
}
export function parseBotState(value: unknown, path: string): BotState {
  const fail = (): never => { throw new TypeError(`Invalid BotState at ${path}`) }
  if (!value || typeof value !== 'object' || Array.isArray(value)) return fail()
  const d = value as BotState
  const keys = Object.keys(createBotState(0, '', { x: 0, y: 0, z: 0 })).sort()
  if (Object.keys(d).sort().join() !== keys.join() || d.version !== BOT_AI_VERSION) return fail()
  const vector = (p: CollisionVector3) => p && Object.keys(p).sort().join() === 'x,y,z' && [p.x, p.y, p.z].every(Number.isFinite)
  for (const key of ['alertTick', 'noticedTick', 'decisionTick', 'strafeTick', 'jumpTick', 'retreatTick', 'supportTick', 'recoveryTick', 'progressTick'] as const) if (!Number.isSafeInteger(d[key]) || d[key] < 0) fail()
  if (!Number.isFinite(d.lastHealth) || d.lastHealth < 0 || !Number.isFinite(d.alertYaw)) fail()
  if (typeof d.intent !== 'string' || typeof d.targetId !== 'string' || !Number.isFinite(d.personality) || d.personality < 0 || d.personality > 1 || ![-1, 1].includes(d.strafeSign) || !vector(d.goal) || !vector(d.progressPosition)) fail()
  if (!Array.isArray(d.route) || !d.route.every(vector) || !Array.isArray(d.memory)) fail()
  for (const m of d.memory) if (!m || Object.keys(m).sort().join() !== 'id,lifeId,position,tick' || typeof m.id !== 'string' || typeof m.lifeId !== 'string' || !vector(m.position) || !Number.isSafeInteger(m.tick) || m.tick < 0) fail()
  if (new Set(d.memory.map(m => m.id)).size !== d.memory.length) fail()
  return { ...d, rng: parseSeededRngState(d.rng), route: d.route.map(p => ({ ...p })), memory: d.memory.map(m => ({ ...m, position: { ...m.position } })), goal: { ...d.goal }, progressPosition: { ...d.progressPosition } }
}
