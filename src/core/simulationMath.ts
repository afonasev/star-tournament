// Numeric contract: collapse platform libm's sub-ULP differences to the
// collision backend's IEEE-754 binary32 precision at transcendental boundaries.
// This precision is a replay invariant, deliberately not a balance control.
export const simulationMath = {
  sin: (x: number): number => Math.fround(Math.sin(x)),
  cos: (x: number): number => Math.fround(Math.cos(x)),
  tan: (x: number): number => Math.fround(Math.tan(x)),
  atan2: (y: number, x: number): number => Math.fround(Math.atan2(y, x)),
  hypot: (...values: number[]): number => Math.fround(Math.hypot(...values)),
}
