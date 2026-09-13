import { describe, expect, it } from 'vitest'
import { localViewportLayout } from './localViewportLayout'

describe('localViewportLayout', () => {
  it('uses full, equal horizontal, and 2x2 layouts without gaps', () => {
    expect(localViewportLayout(1, 100, 80).viewports).toEqual([{ seatIndex: 0, x: 0, y: 0, width: 100, height: 80 }])
    expect(localViewportLayout(2, 100, 80).viewports).toEqual([
      { seatIndex: 0, x: 0, y: 0, width: 50, height: 80 }, { seatIndex: 1, x: 50, y: 0, width: 50, height: 80 },
    ])
    const three = localViewportLayout(3, 101, 81)
    expect(three.viewports).toHaveLength(3)
    expect(three.standingsCell).toEqual({ x: 50, y: 0, width: 51, height: 40 })
    expect(localViewportLayout(4, 100, 80).viewports.every(({ width, height }) => width === 50 && height === 40)).toBe(true)
  })
})
