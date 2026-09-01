import {
  COLLISION_BACKEND_PACKAGE,
  COLLISION_CONTRACT_VERSION,
  COLLISION_ERROR_CODES,
  CollisionContractError,
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
  parseCollisionCompatibilityIdentity,
  type CollisionCompatibilityIdentity,
} from './contracts'

export interface CollisionBackendModule {
  readonly init: () => Promise<void>
  readonly version: () => string
}

export interface CollisionBackendReady {
  readonly identity: CollisionCompatibilityIdentity
  readonly module: CollisionBackendModule
}

export type CollisionBackendModuleLoader = () => Promise<CollisionBackendModule>

export type CollisionBackendInitializer = (
  expectedIdentity?: unknown,
) => Promise<CollisionBackendReady>

function initializationError(): CollisionContractError {
  return new CollisionContractError(
    COLLISION_ERROR_CODES.backendInitializationFailed,
    'Collision backend initialization failed',
  )
}

/**
 * Creates an independently testable initializer. The first supported request
 * owns one cached ready promise, including its deterministic rejection state.
 */
export function createCollisionBackendInitializer(
  loadModule: CollisionBackendModuleLoader,
): CollisionBackendInitializer {
  let readyPromise: Promise<CollisionBackendReady> | undefined

  return (expectedIdentity = RUNTIME_COLLISION_COMPATIBILITY_IDENTITY) => {
    try {
      parseCollisionCompatibilityIdentity(expectedIdentity)
    } catch (error) {
      return Promise.reject(error)
    }

    readyPromise ??= (async () => {
      try {
        const module = await loadModule()
        await module.init()

        const actualIdentity = parseCollisionCompatibilityIdentity({
          contractVersion: COLLISION_CONTRACT_VERSION,
          backendPackage: COLLISION_BACKEND_PACKAGE,
          backendVersion: module.version(),
        })

        return Object.freeze({
          identity: actualIdentity,
          module,
        })
      } catch (error) {
        if (error instanceof CollisionContractError) {
          throw error
        }
        throw initializationError()
      }
    })()

    return readyPromise
  }
}

const initializeDefaultBackend = createCollisionBackendInitializer(async () => {
  const module = await import('@dimforge/rapier3d-deterministic-compat')
  return module
})

/** Lazy production boundary for the exact-pinned collision backend. */
export function initializeCollisionBackend(
  expectedIdentity: unknown = RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
): Promise<CollisionBackendReady> {
  return initializeDefaultBackend(expectedIdentity)
}
