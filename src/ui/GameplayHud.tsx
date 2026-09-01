import type { GameplayUiSnapshot } from './gameplayUiSnapshot'

export interface GameplayHudProps {
  readonly snapshot: Readonly<GameplayUiSnapshot>
  readonly onRequestControl?: () => void
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
  if (status === 'paused') {
    return {
      eyebrow: 'ПАУЗА',
      title: 'Управление приостановлено',
      detail: 'Вернитесь в бой, когда будете готовы.',
      action: 'Продолжить',
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

export function GameplayHud({ snapshot, onRequestControl }: GameplayHudProps) {
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
            <strong>{snapshot.health}</strong>
            <meter min="0" max={snapshot.maximumHealth} value={snapshot.health}>
              {healthPercent}%
            </meter>
          </section>

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
          </dl>
        </aside>
      )}
    </main>
  )
}
