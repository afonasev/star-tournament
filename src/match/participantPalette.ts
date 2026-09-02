import { deepFreeze } from '../core/deepFreeze'

/** Presentation-only vetted colors; resolved values still live in MatchConfiguration. */
export const VETTED_PARTICIPANT_COLORS = deepFreeze([
  '#3b82f6', '#d946ef', '#fb7185', '#8b5cf6', '#10b981',
  '#f59e0b', '#e11d48', '#0ea5e9', '#6366f1', '#14b8a6', '#22c55e', '#a855f7',
] as const)

export const TEAM_COLORS = deepFreeze({ 'team-a': '#3b82f6', 'team-b': '#d946ef' } as const)

export function isVettedParticipantColor(color: string): boolean {
  return (VETTED_PARTICIPANT_COLORS as readonly string[]).includes(color.toLowerCase())
}
