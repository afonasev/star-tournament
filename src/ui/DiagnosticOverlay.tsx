import type { UiSnapshot } from '../runtime/uiSnapshot'

interface DiagnosticOverlayProps {
  readonly snapshot: Readonly<UiSnapshot>
}

const statusLabels = {
  starting: 'Запуск',
  running: 'Симуляция активна',
  stopped: 'Симуляция остановлена',
  error: 'Ошибка запуска',
} as const

function shortHash(hash: string): string {
  return hash.length > 18 ? `${hash.slice(0, 18)}…` : hash
}

export function DiagnosticOverlay({ snapshot }: DiagnosticOverlayProps) {
  const isError = snapshot.status === 'error'

  return (
    <main className="runtime-shell">
      <header className="brand" aria-label="Star Tournament">
        <span className="brand__mark" aria-hidden="true">ST</span>
        <span>
          <strong>STAR TOURNAMENT</strong>
          <small>технический фундамент</small>
        </span>
      </header>

      {isError ? (
        <section className="error-panel" role="alert">
          <p className="eyebrow">WEBGL НЕДОСТУПЕН</p>
          <h1>Не удалось запустить 3D-сцену</h1>
          <p>{snapshot.errorMessage ?? 'Браузер не предоставил совместимый WebGL-контекст.'}</p>
          <p className="error-panel__hint">
            Проверьте аппаратное ускорение браузера и перезагрузите страницу.
          </p>
        </section>
      ) : (
        <section className="diagnostics" aria-label="Диагностика runtime">
          <div className="status-line">
            <span className="status-line__light" aria-hidden="true" />
            <strong>{statusLabels[snapshot.status]}</strong>
          </div>
          <dl>
            <div>
              <dt>Tick</dt>
              <dd>{snapshot.tick.toLocaleString('ru-RU')}</dd>
            </div>
            <div>
              <dt>State</dt>
              <dd title={snapshot.stateHash}>{shortHash(snapshot.stateHash)}</dd>
            </div>
            <div>
              <dt>Profile</dt>
              <dd>{snapshot.profileId}</dd>
              <dd className="hash" title={snapshot.profileHash}>{shortHash(snapshot.profileHash)}</dd>
            </div>
            <div>
              <dt>Arena</dt>
              <dd>{snapshot.arenaId}</dd>
              <dd className="hash" title={snapshot.arenaHash}>{shortHash(snapshot.arenaHash)}</dd>
            </div>
          </dl>
        </section>
      )}

      <footer className="runtime-caption">
        <span>DIAGNOSTIC ARENA</span>
        <span>FIXED STEP · 60 HZ</span>
      </footer>
    </main>
  )
}
