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
  const benchmark = snapshot.collision.benchmark

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
          <p className="eyebrow">RUNTIME НЕДОСТУПЕН</p>
          <h1>Не удалось запустить прототип</h1>
          <p>{snapshot.errorMessage ?? 'Браузер не предоставил необходимые runtime-возможности.'}</p>
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
            <div>
              <dt>Collision</dt>
              <dd>{snapshot.collision.status} · {snapshot.collision.fixtureStatus}</dd>
              <dd className="hash" title={snapshot.collision.identity}>{snapshot.collision.identity}</dd>
              {snapshot.collision.checkpointHash && (
                <dd className="hash" title={snapshot.collision.checkpointHash}>
                  {shortHash(snapshot.collision.checkpointHash)}
                </dd>
              )}
              {snapshot.collision.initLatencyMilliseconds && (
                <dd className="hash">
                  init {snapshot.collision.initLatencyMilliseconds.cold.toFixed(2)} / warm {snapshot.collision.initLatencyMilliseconds.warm.toFixed(2)} ms
                </dd>
              )}
            </div>
            {benchmark && (
              <div>
                <dt>Collision p95 / p99 / max</dt>
                <dd>{benchmark.p95Milliseconds.toFixed(3)} / {benchmark.p99Milliseconds.toFixed(3)} / {benchmark.maxMilliseconds.toFixed(3)} ms</dd>
                <dd className="hash">{benchmark.sampleCount.toLocaleString('ru-RU')} samples</dd>
              </div>
            )}
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
