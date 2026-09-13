export interface LocalViewport {
  readonly seatIndex: number
  readonly x: number
  readonly y: number
  readonly width: number
  readonly height: number
}

export interface LocalViewportLayout {
  readonly viewports: readonly LocalViewport[]
  readonly standingsCell: Omit<LocalViewport, 'seatIndex'> | null
}

/** CSS-independent backing-buffer rectangles, ordered by stable local-seat ID. */
export function localViewportLayout(seatCount: number, width: number, height: number): LocalViewportLayout {
  if (!Number.isInteger(seatCount) || seatCount < 1 || seatCount > 4) throw new RangeError('seatCount must be 1-4')
  if (!Number.isInteger(width) || !Number.isInteger(height) || width <= 0 || height <= 0) throw new RangeError('viewport size must be positive integers')
  if (seatCount === 1) return { viewports: [{ seatIndex: 0, x: 0, y: 0, width, height }], standingsCell: null }
  if (seatCount === 2) {
    const leftWidth = Math.floor(width / 2)
    return { viewports: [
      { seatIndex: 0, x: 0, y: 0, width: leftWidth, height },
      { seatIndex: 1, x: leftWidth, y: 0, width: width - leftWidth, height },
    ], standingsCell: null }
  }
  const leftWidth = Math.floor(width / 2); const bottomHeight = Math.floor(height / 2)
  const cells = [
    { x: 0, y: bottomHeight, width: leftWidth, height: height - bottomHeight },
    { x: leftWidth, y: bottomHeight, width: width - leftWidth, height: height - bottomHeight },
    { x: 0, y: 0, width: leftWidth, height: bottomHeight },
    { x: leftWidth, y: 0, width: width - leftWidth, height: bottomHeight },
  ]
  return { viewports: cells.slice(0, seatCount).map((cell, seatIndex) => ({ seatIndex, ...cell })), standingsCell: seatCount === 3 ? cells[3]! : null }
}
