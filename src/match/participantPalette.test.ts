import { describe, expect, it } from 'vitest'

import { TEAM_COLORS, VETTED_PARTICIPANT_COLORS, isVettedParticipantColor } from './participantPalette'

describe('participant palette', () => {
  it('offers more than eight vetted colors and a distinct Team A/B pair', () => {
    expect(VETTED_PARTICIPANT_COLORS.length).toBeGreaterThan(8)
    expect(new Set(VETTED_PARTICIPANT_COLORS).size).toBe(VETTED_PARTICIPANT_COLORS.length)
    expect(TEAM_COLORS['team-a']).not.toBe(TEAM_COLORS['team-b'])
    expect(isVettedParticipantColor(TEAM_COLORS['team-a'])).toBe(true)
    expect(isVettedParticipantColor(TEAM_COLORS['team-b'])).toBe(true)
  })
})
