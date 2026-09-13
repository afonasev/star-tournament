// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { GRAPHICS_QUALITY_STORAGE_KEY } from '../presentation'
import { MatchSetupMenu } from './MatchSetupMenu'

afterEach(() => { cleanup(); localStorage.clear() })

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
    for (let index = 1; index <= 4; index += 1) fireEvent.change(screen.getByLabelText(`Команда участника ${index}`), { target: { value: 'team-a' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Каждый за себя' }))
    fireEvent.change(screen.getByLabelText('Длительность матча'), { target: { value: '31' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Длительность матча'), { target: { value: '5' } })
    fireEvent.click(screen.getByLabelText('Включить цель по очкам'))
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Цель по очкам' }), { target: { value: '1050' } })
    expect(screen.getByRole('button', { name: 'Начать матч' })).toBeDisabled()
  })

  it('uses medium by default and accepts a manual size and uint32 seed', () => {
    const onStart = vi.fn()
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={onStart} />)
    expect(screen.getByLabelText('Размер арены')).toHaveValue('medium')
    fireEvent.change(screen.getByLabelText('Размер арены'), { target: { value: 'large' } })
    fireEvent.change(screen.getByLabelText('Seed арены'), { target: { value: '4294967295' } })
    fireEvent.click(screen.getByRole('button', { name: 'Начать матч' }))
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ arenaSize: 'large', arenaSeed: 0xffffffff }))
  })

  it('blocks invalid seed and exposes all generation recovery actions', () => {
    const onStart = vi.fn()
    const onUseFallback = vi.fn()
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={onStart} onUseFallback={onUseFallback} generationError={{ seed: 7, size: 'medium', reason: 'MODE_FAIRNESS_FAILED' }} />)
    fireEvent.change(screen.getByLabelText('Seed арены'), { target: { value: '-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Начать матч' }))
    expect(onStart).not.toHaveBeenCalled()
    expect(screen.getByText(/Seed должен быть/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Повторить seed' }))
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ arenaSeed: 7 }))
    expect(screen.getByRole('button', { name: 'Новый seed' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Запустить запасную арену' }))
    expect(onUseFallback).toHaveBeenCalledOnce()
  })

  it('persists accessible graphics controls and reports a safe malformed-preference fallback', () => {
    const onGraphicsQualityChange = vi.fn()
    localStorage.setItem(GRAPHICS_QUALITY_STORAGE_KEY, '{malformed')
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={vi.fn()} onGraphicsQualityChange={onGraphicsQualityChange} />)
    expect(screen.getByText(/Неверная локальная настройка/)).toHaveTextContent(/Balanced/)
    const details = document.querySelector('details')!
    fireEvent.click(details.querySelector('summary')!)
    fireEvent.change(screen.getByLabelText('Пресет графики'), { target: { value: 'ultra' } })
    fireEvent.change(screen.getByLabelText('Render scale'), { target: { value: '1.25' } })
    fireEvent.click(screen.getByLabelText('Decals'))
    expect(screen.getByLabelText('Effective graphics quality')).toHaveTextContent(/Ultra.*×1.25.*decals off/)
    expect(onGraphicsQualityChange).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'ultra', renderScale: 1.25, decals: false }))
    expect(localStorage.getItem(GRAPHICS_QUALITY_STORAGE_KEY)).toContain('graphics-quality-v1')
    expect(screen.queryByLabelText('Post-processing')).not.toBeDisabled()
    expect(document.getElementById('geometry-lod-hint')).toHaveTextContent(/LOD 0.*лучше силуэты.*выше нагрузка.*LOD 1.*больше производительности/)
    expect(document.getElementById('decals-hint')).toHaveTextContent(/разметку маршрутов.*богаче.*Отключите.*производительность/)
    expect(document.getElementById('post-processing-hint')).toHaveTextContent(/финальных экранных эффектов.*ещё не подключён.*не меняет изображение и нагрузку/)
    expect(screen.getByLabelText('Geometry LOD')).toHaveAttribute('aria-describedby', 'geometry-lod-hint')
    expect(screen.getByLabelText('Decals')).toHaveAttribute('aria-describedby', 'decals-hint')
    expect(screen.getByLabelText('Post-processing')).toHaveAttribute('aria-describedby', 'post-processing-hint')
  })

  it('offers fullscreen entry alongside graphics settings', () => {
    const onToggleFullscreen = vi.fn()
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={vi.fn()} onToggleFullscreen={onToggleFullscreen} />)
    const details = document.querySelector('details')!
    fireEvent.click(details.querySelector('summary')!)
    fireEvent.click(screen.getByRole('button', { name: 'Во весь экран' }))
    expect(onToggleFullscreen).toHaveBeenCalledOnce()
  })
})
