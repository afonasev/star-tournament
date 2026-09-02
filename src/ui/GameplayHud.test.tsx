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
    matchPhase: 'running',
    remainingTicks: 18_000,
    timerText: '05:00',
    scoreboardVisible: false,
    standings: { mode: 'ffa', rows: [] },
    result: null,
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
    expect(screen.getByLabelText('Время матча')).toHaveTextContent('05:00')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('renders live standings while held and groups team totals', () => {
    render(<GameplayHud snapshot={snapshot({
      scoreboardVisible: true,
      standings: { mode: 'teams', teams: [{ team: 'team-a', color: '#ef4444', totalScore: 100, participants: [{ rank: 1, semanticId: 'a', name: 'Ace', kind: 'local-seat', color: '#ef4444', team: 'team-a', kills: 1, assists: 0, deaths: 0, damageDealt: 123.6, damageReceived: 45.2, score: 100 }] }, { team: 'team-b', color: '#3b82f6', totalScore: 0, participants: [] }] },
    })} />)
    expect(screen.getByLabelText('Таблица результатов')).toHaveTextContent('Team A 100')
    expect(screen.getAllByRole('columnheader', { name: 'Очки' })).toHaveLength(2)
    expect(screen.getByRole('rowheader', { name: /Ace/ }).closest('tr')).toHaveAttribute('data-human', 'true')
    expect(screen.getByText('ИГРОК')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '124' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '45' })).toBeInTheDocument()
  })

  it('offers complete pause and draw-free result actions', () => {
    const onRepeat = vi.fn()
    const onMainMenu = vi.fn()
    const { rerender } = render(<GameplayHud snapshot={snapshot({ status: 'paused' })} onRepeat={onRepeat} onMainMenu={onMainMenu} />)
    expect(screen.getByRole('button', { name: 'Полноэкранный режим' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Повторить матч' }))
    expect(onRepeat).toHaveBeenCalledOnce()
    const result = { winnerType: 'participant' as const, winnerId: 'local-player-1', trigger: 'time-limit' as const, finalTick: 10, finalStandings: ['local-player-1'] }
    rerender(<GameplayHud snapshot={snapshot({ status: 'results', matchPhase: 'finished', result })} onMainMenu={onMainMenu} />)
    expect(screen.getByText('Победитель: local-player-1')).toBeInTheDocument()
    expect(screen.queryByText(/ничья/i)).not.toBeInTheDocument()
    const teamResult = { ...result, winnerType: 'team' as const, winnerId: 'team-a' }
    rerender(<GameplayHud snapshot={snapshot({ status: 'results', matchPhase: 'finished', result: teamResult, standings: { mode: 'teams', teams: [{ team: 'team-a', color: '#ef4444', totalScore: 100, participants: [] }, { team: 'team-b', color: '#3b82f6', totalScore: 0, participants: [] }] } })} />)
    expect(screen.getByText('Победитель: Team A')).toBeInTheDocument()
    expect(screen.getByText('Team A', { selector: 'h2' }).closest('section')).toHaveAttribute('data-winner', 'true')
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
            scenarioIdentity: 'scenario-v2',
            configurationIdentity: 'local-match-v1',
            matchPhase: 'running',
            remainingTicks: 17_958,
            playerPosition: { x: 1.25, y: 0.9, z: -3.5 },
            playerYawDegrees: 45,
            playerPitchDegrees: -10,
            playerGrounded: true,
            ammo: 12,
            targets: [
              { semanticId: 'target-alpha', health: 70, active: true },
              { semanticId: 'target-beta', health: 0, active: false },
            ],
            participantStatistics: [{ semanticId: 'local-player-1', kills: 1, assists: 2, deaths: 3, score: 450 }],
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
