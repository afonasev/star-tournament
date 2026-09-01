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
  collision: {
    status: 'ready',
    identity: 'collision-contract-v1 · deterministic-compat@0.20.0',
    fixtureId: 'collision-fixture-v1',
    fixtureStatus: 'passed',
    checkpointHash: 'fnv1a64-v1:collision',
  },
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
    expect(screen.getByText('ready · passed')).toBeInTheDocument()
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

    expect(screen.getByRole('alert')).toHaveTextContent('Не удалось запустить прототип')
    expect(screen.getByRole('alert')).toHaveTextContent('WebGL context creation failed')
    expect(screen.queryByLabelText('Диагностика runtime')).not.toBeInTheDocument()
  })
})
