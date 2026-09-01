// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import type { UiSnapshot } from '../runtime/uiSnapshot'
import { DiagnosticOverlay } from './DiagnosticOverlay'

const runningSnapshot: UiSnapshot = {
  status: 'running',
  tick: 42,
  stateHash: 'fnv1a64:statehash',
  profileId: 'prototype-v1',
  profileHash: 'fnv1a64:profilehash',
  arenaId: 'diagnostic-arena-v1',
  arenaHash: 'fnv1a64:arenahash',
}

afterEach(cleanup)

describe('DiagnosticOverlay', () => {
  it('renders only immutable snapshot values and identities', () => {
    render(<DiagnosticOverlay snapshot={Object.freeze(runningSnapshot)} />)

    expect(screen.getByText('Симуляция активна')).toBeInTheDocument()
    expect(screen.getByText('42')).toBeInTheDocument()
    expect(screen.getByText('prototype-v1')).toBeInTheDocument()
    expect(screen.getByText('diagnostic-arena-v1')).toBeInTheDocument()
    expect(screen.getByText('fnv1a64:statehash')).toBeInTheDocument()
  })

  it('renders an actionable WebGL error panel', () => {
    render(
      <DiagnosticOverlay
        snapshot={{
          ...runningSnapshot,
          status: 'error',
          errorMessage: 'WebGL context creation failed',
        }}
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Не удалось запустить 3D-сцену')
    expect(screen.getByRole('alert')).toHaveTextContent('WebGL context creation failed')
    expect(screen.queryByLabelText('Диагностика runtime')).not.toBeInTheDocument()
  })
})
