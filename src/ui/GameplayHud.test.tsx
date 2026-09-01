// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { GameplayHud } from './GameplayHud'
import { createGameplayUiSnapshot } from './gameplayUiSnapshot'

function snapshot(
  overrides: Partial<Parameters<typeof createGameplayUiSnapshot>[0]> = {},
) {
  return createGameplayUiSnapshot({
    status: 'running',
    health: 100,
    maximumHealth: 100,
    weaponName: 'Двухстволка',
    ammo: 20,
    crosshairFeedback: 'idle',
    ...overrides,
  })
}

afterEach(cleanup)

describe('GameplayHud', () => {
  it('keeps the center compact while exposing health, weapon and ammo semantics', () => {
    render(<GameplayHud snapshot={snapshot({ health: 70, ammo: 12 })} />)

    expect(screen.getByLabelText('Здоровье игрока')).toHaveTextContent('70')
    expect(screen.getByRole('meter')).toHaveAttribute('max', '100')
    expect(screen.getByRole('meter')).toHaveAttribute('value', '70')
    expect(screen.getByLabelText('Оружие и боезапас')).toHaveTextContent('Двухстволка')
    expect(screen.getByLabelText('Оружие и боезапас')).toHaveTextContent('12')
    expect(screen.getByLabelText('Прицел')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('shows hit and dry-fire feedback from immutable presentation state', () => {
    const { rerender } = render(
      <GameplayHud snapshot={snapshot({ crosshairFeedback: 'hit' })} />,
    )
    expect(screen.getByLabelText('Попадание')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Попадание')

    rerender(<GameplayHud snapshot={snapshot({ crosshairFeedback: 'dry-fire' })} />)
    expect(screen.getByLabelText('Нет патронов')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Нет патронов')
  })

  it('distinguishes initial start and resume overlays and requests pointer control', () => {
    const onRequestControl = vi.fn()
    const { rerender } = render(
      <GameplayHud snapshot={snapshot({ status: 'start' })} onRequestControl={onRequestControl} />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Начать игру' }))
    expect(onRequestControl).toHaveBeenCalledTimes(1)

    rerender(
      <GameplayHud snapshot={snapshot({ status: 'paused' })} onRequestControl={onRequestControl} />,
    )
    expect(screen.getByRole('button', { name: 'Продолжить' })).toBeInTheDocument()

    rerender(
      <GameplayHud
        snapshot={snapshot({ status: 'paused', errorMessage: 'Pointer lock failed' })}
        onRequestControl={onRequestControl}
      />,
    )
    expect(screen.getByText('Pointer lock failed')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
    expect(onRequestControl).toHaveBeenCalledTimes(2)
  })

  it('keeps debug identities opt-in and exposes runtime errors accessibly', () => {
    const { rerender } = render(<GameplayHud snapshot={snapshot()} />)
    expect(screen.queryByLabelText('Отладочная информация')).not.toBeInTheDocument()

    rerender(
      <GameplayHud
        snapshot={snapshot({
          debug: {
            tick: 42,
            stateHash: 'fnv1a64-v1:state',
            profileIdentity: 'prototype-v1@2:profile',
            arenaIdentity: 'diagnostic-arena:arena',
            collisionIdentity: 'collision-contract-v1@0.20.0',
            playerPosition: { x: 1.25, y: 0.9, z: -3.5 },
            playerYawDegrees: 45,
            playerPitchDegrees: -10,
            playerGrounded: true,
            ammo: 12,
            targets: [
              { semanticId: 'target-alpha', health: 70, active: true },
              { semanticId: 'target-beta', health: 0, active: false },
            ],
          },
        })}
      />,
    )
    const debug = screen.getByLabelText('Отладочная информация')
    expect(debug).toHaveTextContent('DEBUG · TICK 42')
    expect(debug).toHaveTextContent('1.25, 0.90, -3.50')
    expect(debug).toHaveTextContent('45.0° / -10.0°')
    expect(debug).toHaveTextContent('Groundedyes')
    expect(debug).toHaveTextContent('Ammo12')
    expect(debug).toHaveTextContent('target-alpha70.0')
    expect(debug).toHaveTextContent('target-betadestroyed')

    rerender(
      <GameplayHud
        snapshot={snapshot({ status: 'error', errorMessage: 'WebGL unavailable' })}
      />,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('WebGL unavailable')
  })
})
