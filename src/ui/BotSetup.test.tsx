// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MatchSetupMenu } from './MatchSetupMenu'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import type { MatchConfiguration } from '../match'

afterEach(cleanup)
describe('bot setup',()=>{
  it('enforces the browser participant limit and keeps a human seat',()=>{
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={()=>{}}/>)
    for(let i=0;i<4;i++)fireEvent.click(screen.getByText('Добавить бота'))
    expect((screen.getByText('Добавить бота') as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getAllByLabelText(/^Сложность бота/)).toHaveLength(7)
    for(let i=7;i>=1;i--)fireEvent.click(screen.getByLabelText(`Удалить бота ${i}`))
    expect((screen.getByText('Начать матч') as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByLabelText('Имя участника 1')).toBeDefined()
  })
  it('edits difficulty independently, preserves row identities and submits the roster',()=>{
    let started:MatchConfiguration|undefined
    render(<MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={c=>{started=c}}/>)
    fireEvent.change(screen.getByLabelText('Сложность бота 1'),{target:{value:'easy'}})
    fireEvent.change(screen.getByLabelText('Сложность бота 3'),{target:{value:'hard'}})
    fireEvent.click(screen.getByLabelText('Удалить бота 2'))
    expect((screen.getByLabelText('Сложность бота 2') as HTMLSelectElement).value).toBe('hard')
    fireEvent.click(screen.getByText('Добавить бота'))
    expect((screen.getByLabelText('Сложность бота 3') as HTMLSelectElement).value).toBe('normal')
    fireEvent.click(screen.getByText('Начать матч'))
    expect(started?.participants.filter(p=>p.kind==='bot').map(p=>p.difficulty).sort()).toEqual(['easy','hard','normal'])
  })
})
