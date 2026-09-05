import type { CSSProperties } from 'react'

import type { GameplayUiSnapshot } from './gameplayUiSnapshot'
import type { StandingsParticipantRow } from '../match'

export interface GameplayHudProps {
  readonly snapshot: Readonly<GameplayUiSnapshot>
  readonly onRequestControl?: () => void
  readonly onRepeat?: () => void
  readonly onMainMenu?: () => void
  readonly onToggleFullscreen?: () => void
}

function overlayCopy(status: GameplayUiSnapshot['status']): Readonly<{
  eyebrow: string
  title: string
  detail: string
  action: string
}> | null {
  if (status === 'start') {
    return {
      eyebrow: 'STAR TOURNAMENT',
      title: 'Войти на арену',
      detail: 'WASD — движение · мышь — обзор · Space — прыжок · ЛКМ — огонь',
      action: 'Начать игру',
    }
  }
  return null
}

function shortIdentity(identity: string): string {
  return identity.length > 30 ? `${identity.slice(0, 30)}…` : identity
}

function formatVectorComponent(value: number): string {
  return value.toFixed(2)
}

function formatAngle(value: number): string {
  return `${value.toFixed(1)}°`
}

function formatDamage(value: number): string {
  return String(Math.round(value))
}

function StandingsTable({ snapshot }: { readonly snapshot: Readonly<GameplayUiSnapshot> }) {
  const table = (rows: readonly StandingsParticipantRow[]) => (
    <table>
      <colgroup><col className="gameplay-hud__column-rank" /><col className="gameplay-hud__column-participant" /><col span={3} className="gameplay-hud__column-stat" /><col span={2} className="gameplay-hud__column-damage" /><col className="gameplay-hud__column-score" /></colgroup>
      <thead><tr><th>#</th><th>Участник</th><th>K</th><th>A</th><th>D</th><th>Урон</th><th>Получено</th><th>Очки</th></tr></thead>
      <tbody>{rows.map((row) => {
        const isHuman = row.kind === 'local-seat'
        return <tr key={row.semanticId} data-leader={row.rank === 1} data-human={isHuman} style={{ '--participant-color': row.color } as CSSProperties}><td>{row.rank}</td><th scope="row"><span>{row.name}</span>{isHuman && <small className="gameplay-hud__human-badge">ИГРОК</small>}</th><td>{row.kills}</td><td>{row.assists}</td><td>{row.deaths}</td><td>{formatDamage(row.damageDealt)}</td><td>{formatDamage(row.damageReceived)}</td><td>{row.score}</td></tr>
      })}</tbody>
    </table>
  )
  return <section className="gameplay-hud__scoreboard" aria-label="Таблица результатов"><p>LIVE STANDINGS</p>{snapshot.standings.mode === 'ffa' ? table(snapshot.standings.rows) : snapshot.standings.teams.map((team) => <section key={team.team} className="gameplay-hud__team" data-team={team.team} data-winner={snapshot.result?.winnerType === 'team' && snapshot.result.winnerId === team.team}><h2 style={{ color: team.color }}>{team.team === 'team-a' ? 'Team A' : 'Team B'} <strong>{team.totalScore}</strong></h2>{table(team.participants)}</section>)}</section>
}

function winnerLabel(snapshot: Readonly<GameplayUiSnapshot>): string {
  const result = snapshot.result
  if (result === null) return ''
  if (result.winnerType === 'team') return result.winnerId === 'team-a' ? 'Team A' : 'Team B'
  const rows = snapshot.standings.mode === 'ffa'
    ? snapshot.standings.rows
    : snapshot.standings.teams.flatMap(({ participants }) => participants)
  return rows.find(({ semanticId }) => semanticId === result.winnerId)?.name ?? result.winnerId
}

export function GameplayHud({ snapshot, onRequestControl, onRepeat, onMainMenu, onToggleFullscreen }: GameplayHudProps) {
  const overlay = overlayCopy(snapshot.status)
  const healthPercent = snapshot.maximumHealth === 0
    ? 0
    : Math.round((snapshot.health / snapshot.maximumHealth) * 100)
  const feedbackLabel = snapshot.crosshairFeedback === 'hit'
    ? 'Попадание'
    : snapshot.crosshairFeedback === 'dry-fire'
      ? 'Нет патронов'
      : ''

  return (
    <main className="gameplay-hud" data-status={snapshot.status}>
      {snapshot.status !== 'error' && (
        <>
          <section className="gameplay-hud__health" aria-label="Здоровье игрока">
            <span className="gameplay-hud__label">HP</span>
            <strong>{Math.ceil(snapshot.health)}</strong>
            <meter min="0" max={snapshot.maximumHealth} value={snapshot.health}>
              {healthPercent}%
            </meter>
          </section>

          <time className="gameplay-hud__timer" aria-label="Время матча" data-overtime={snapshot.matchPhase === 'overtime'}>{snapshot.timerText}</time>

          <div
            className={`gameplay-hud__crosshair gameplay-hud__crosshair--${snapshot.crosshairFeedback}`}
            role="img"
            aria-label={feedbackLabel || 'Прицел'}
          >
            <span aria-hidden="true" />
            <span aria-hidden="true" />
            <span aria-hidden="true" />
            <span aria-hidden="true" />
          </div>

          <p className="gameplay-hud__feedback" role="status" aria-live="polite">
            {feedbackLabel}
          </p>

          <section className="gameplay-hud__weapon" aria-label="Оружие и боезапас">
            <span className="gameplay-hud__weapon-icon" aria-hidden="true">◫</span>
            <span>
              <small>{snapshot.weaponName}</small>
              <strong>{snapshot.ammo}</strong>
            </span>
          </section>
        </>
      )}

      {overlay && (
        <section className="gameplay-hud__overlay" aria-label={overlay.title}>
          <p>{overlay.eyebrow}</p>
          <h1>{overlay.title}</h1>
          <small>{snapshot.errorMessage ?? overlay.detail}</small>
          <button type="button" onClick={onRequestControl}>{overlay.action}</button>
        </section>
      )}

      {snapshot.scoreboardVisible && <StandingsTable snapshot={snapshot} />}
      {snapshot.status === 'running' && snapshot.deathMessage && <p className="gameplay-hud__death" role="status">{snapshot.deathMessage}<small>Возрождение…</small></p>}

      {snapshot.status === 'paused' && (
        <section className="gameplay-hud__overlay gameplay-hud__pause" aria-label="Меню паузы">
          <p>ПАУЗА</p><h1>Матч приостановлен</h1>
          <small>{snapshot.errorMessage ?? 'Таймер и симуляция остановлены.'}</small>
          <div className="gameplay-hud__actions">
            <button type="button" onClick={onRequestControl}>Продолжить</button>
            <button type="button" onClick={onRepeat}>Повторить матч</button>
            <button type="button" onClick={onToggleFullscreen}>Полноэкранный режим</button>
            <button type="button" onClick={onMainMenu}>Главное меню</button>
          </div>
        </section>
      )}

      {snapshot.status === 'results' && snapshot.result && (
        <section className="gameplay-hud__overlay gameplay-hud__results" aria-label="Результаты матча">
          <p>{snapshot.result.trigger === 'score-limit' ? 'ЛИМИТ ОЧКОВ' : 'ВРЕМЯ ИСТЕКЛО'}</p>
          <h1>Победитель: {winnerLabel(snapshot)}</h1>
          <StandingsTable snapshot={snapshot} />
          <div className="gameplay-hud__actions"><button type="button" onClick={onRepeat}>Повторить матч</button><button type="button" onClick={onMainMenu}>Главное меню</button></div>
        </section>
      )}

      {snapshot.status === 'error' && (
        <section className="gameplay-hud__error" role="alert">
          <p>RUNTIME НЕДОСТУПЕН</p>
          <h1>Не удалось запустить прототип</h1>
          <span>{snapshot.errorMessage}</span>
        </section>
      )}

      {snapshot.debug && (
        <aside className="gameplay-hud__debug" aria-label="Отладочная информация">
          <strong>DEBUG · TICK {snapshot.debug.tick}</strong>
          <dl>
            <div><dt>State</dt><dd title={snapshot.debug.stateHash}>{shortIdentity(snapshot.debug.stateHash)}</dd></div>
            <div><dt>Profile</dt><dd title={snapshot.debug.profileIdentity}>{shortIdentity(snapshot.debug.profileIdentity)}</dd></div>
            <div><dt>Arena</dt><dd title={snapshot.debug.arenaIdentity}>{shortIdentity(snapshot.debug.arenaIdentity)}</dd></div>
            <div><dt>Collision</dt><dd title={snapshot.debug.collisionIdentity}>{shortIdentity(snapshot.debug.collisionIdentity)}</dd></div>
            <div><dt>Scenario</dt><dd title={snapshot.debug.scenarioIdentity}>{shortIdentity(snapshot.debug.scenarioIdentity)}</dd></div>
            <div><dt>Config</dt><dd title={snapshot.debug.configurationIdentity}>{shortIdentity(snapshot.debug.configurationIdentity)}</dd></div>
            <div><dt>Match</dt><dd>{snapshot.debug.matchPhase} · {snapshot.debug.remainingTicks}</dd></div>
            <div>
              <dt>Position</dt>
              <dd>{[
                snapshot.debug.playerPosition.x,
                snapshot.debug.playerPosition.y,
                snapshot.debug.playerPosition.z,
              ].map(formatVectorComponent).join(', ')}</dd>
            </div>
            <div>
              <dt>Aim</dt>
              <dd>{formatAngle(snapshot.debug.playerYawDegrees)} / {formatAngle(snapshot.debug.playerPitchDegrees)}</dd>
            </div>
            <div><dt>Grounded</dt><dd>{snapshot.debug.playerGrounded ? 'yes' : 'no'}</dd></div>
            <div><dt>Ammo</dt><dd>{snapshot.debug.ammo}</dd></div>
            {snapshot.debug.targets.map((target) => (
              <div key={target.semanticId}>
                <dt>{target.semanticId}</dt>
                <dd>{target.active ? target.health.toFixed(1) : 'destroyed'}</dd>
              </div>
            ))}
            {snapshot.debug.participantStatistics.map((participant) => <div key={`stats:${participant.semanticId}`}><dt>{participant.semanticId}</dt><dd>{participant.score} · {participant.kills}/{participant.assists}/{participant.deaths}</dd></div>)}
          </dl>
        </aside>
      )}
    </main>
  )
}
