// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { MatchSetupMenu } from './MatchSetupMenu'

afterEach(cleanup)

describe('MatchSetupMenu', () => {
  it('creates the exact FFA configuration and nullable score target from profile metadata', () => {
    const onStart = vi.fn()
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={onStart} />)
    fireEvent.change(screen.getByLabelText('Имя участника 1'), { target: { value: 'Ace' } })
    fireEvent.click(screen.getByLabelText('Включить цель по очкам'))
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Цель по очкам' }), { target: { value: '4200' } })
    fireEvent.click(screen.getByRole('button', { name: 'Начать матч' }))
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ mode: 'ffa', durationMinutes: 5, scoreTargetPoints: 4_200 }))
    expect(onStart.mock.calls[0]?.[0].participants[0].name).toBe('Ace')
  })

  it('blocks empty teams and out-of-range or off-step values', () => {
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: 'Команды' }))
    for (let index = 1; index <= 4; index += 1) fireEvent.change(screen.getByLabelText(`Команда участника ${index}`), { target: { value: 'red' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Каждый за себя' }))
    fireEvent.change(screen.getByLabelText('Длительность матча'), { target: { value: '31' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Длительность матча'), { target: { value: '5' } })
    fireEvent.click(screen.getByLabelText('Включить цель по очкам'))
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Цель по очкам' }), { target: { value: '1050' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
  })
})
