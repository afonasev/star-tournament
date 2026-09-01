/** Deep-freezes a data tree while preserving its static type. */
export function deepFreeze<T>(value: T): T {
  if (value === null || typeof value !== 'object' || Object.isFrozen(value)) {
    return value
  }

  for (const key of Reflect.ownKeys(value)) {
    const child = (value as Record<PropertyKey, unknown>)[key]
    deepFreeze(child)
  }

  return Object.freeze(value)
}
