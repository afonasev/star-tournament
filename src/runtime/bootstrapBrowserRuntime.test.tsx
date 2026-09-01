// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { COLLISION_ERROR_CODES, CollisionContractError } from '../collision/contracts'
import { activeRapierCollisionWorldCount } from '../collision/rapierWorld'
import type { DiagnosticRenderer } from '../render/diagnosticRenderer'
import { bootstrapBrowserRuntime, type BrowserApplication } from './bootstrapBrowserRuntime'

function fakeRenderer(): DiagnosticRenderer {
  return {
    arenaId: 'collision-fixture-v1',
    arenaHash: 'fnv1a64-v1:9fb08afcdfaf94e9',
    render: vi.fn(),
    resize: vi.fn(),
    dispose: vi.fn(),
  }
}

let application: BrowserApplication | undefined

beforeEach(() => {
  vi.spyOn(window, 'requestAnimationFrame').mockReturnValue(1)
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => undefined)
})

afterEach(() => {
  application?.dispose()
  application = undefined
  cleanup()
  document.body.replaceChildren()
  window.history.replaceState({}, '', '/')
  vi.restoreAllMocks()
})

describe('browser collision bootstrap', () => {
  it('does not start a partial runtime when collision initialization fails', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const createRenderer = vi.fn(() => fakeRenderer())

    application = await bootstrapBrowserRuntime(mount, {
      createCollisionWorld: async () => {
        throw new CollisionContractError(
          COLLISION_ERROR_CODES.backendInitializationFailed,
          'volatile backend detail must not reach UI',
        )
      },
      createRenderer,
    })

    expect(application.runtime).toBeUndefined()
    expect(createRenderer).not.toHaveBeenCalled()
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(
      'COLLISION_BACKEND_INITIALIZATION_FAILED',
    ))
    expect(screen.queryByText(/volatile backend detail/)).not.toBeInTheDocument()
  })

  it('starts only after a real fixture world is ready and frees it on pagehide', async () => {
    const baseline = activeRapierCollisionWorldCount()
    const mount = document.createElement('div')
    document.body.append(mount)

    application = await bootstrapBrowserRuntime(mount, {
      createRenderer: () => fakeRenderer(),
    })

    expect(application.runtime?.isRunning()).toBe(true)
    expect(activeRapierCollisionWorldCount()).toBe(baseline + 1)
    await waitFor(() => expect(screen.getByText('ready · passed')).toBeInTheDocument())

    window.dispatchEvent(new Event('pagehide'))
    expect(application.runtime?.isRunning()).toBe(false)
    expect(activeRapierCollisionWorldCount()).toBe(baseline)
    expect(mount).toBeEmptyDOMElement()
  })
})

