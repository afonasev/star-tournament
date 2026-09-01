// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { BrowserApplication, BrowserBootstrapDependencies } from '../runtime/bootstrapBrowserRuntime'
import { StarTournamentApp } from './StarTournamentApp'

afterEach(cleanup)

describe('StarTournamentApp session owner', () => {
  it('disposes exactly one prior session on repeat and menu transitions', async () => {
    const applications: { dispose: ReturnType<typeof vi.fn> }[] = []
    const calls: BrowserBootstrapDependencies[] = []
    const bootstrap = vi.fn(async (_mount: HTMLElement, dependencies: BrowserBootstrapDependencies): Promise<BrowserApplication> => {
      calls.push(dependencies)
      const application = { requestControl: async () => true, dispose: vi.fn() }
      applications.push(application)
      return application
    })
    render(<StarTournamentApp bootstrap={bootstrap} />)
    fireEvent.click(screen.getByRole('button', { name: 'Начать матч' }))
    await waitFor(() => expect(bootstrap).toHaveBeenCalledTimes(1))
    const configurationHash = calls[0]?.configuration?.contentHash
    await act(async () => { calls[0]?.onRepeat?.() })
    await waitFor(() => expect(bootstrap).toHaveBeenCalledTimes(2))
    expect(applications[0]?.dispose).toHaveBeenCalledOnce()
    expect(calls[1]?.configuration?.contentHash).toBe(configurationHash)
    await act(async () => { calls[1]?.onMainMenu?.() })
    expect(await screen.findByRole('heading', { name: 'Настройка матча' })).toBeInTheDocument()
    expect(applications[1]?.dispose).toHaveBeenCalledOnce()
  })
})
