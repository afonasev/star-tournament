# 05 — Modular GLB arena kit

Статус: завершено и интегрировано в main; OpenSpec архивирован в `openspec/changes/archive/2026-09-04-modular-glb-arena-kit/`. Ниже сохранены результаты исходной проверки; указанные dev URL — исторические, не текущие стенды. Деплой не выполнен.

## Verification

- Dev stand: `http://127.0.0.1:5187/?muted=1` (HTTP 200).
- Muted in-app Browser: small, medium and large arenas reached the first-person entry overlay with a readable HUD and visible arena; medium also reached the retryable pointer-lock failure/pause surface. The in-app Browser rejected pointer lock, so physical WASD/mouse input remains unverified there.
- 2026-09-04: `npm test` passed (48 files, 287 tests; long 10 000-tick determinism test completed in 57.03 s). Focused GLB/renderer/runtime coverage includes required-asset failure with no renderer, runtime or RAF loop.
- `npm run typecheck`, `npm run build`, strict OpenSpec validation and `npm run perf:browser` passed. Portable lifecycle gates passed; active simulation was 59.49–59.99 ticks/s, submissions no more than 59.99/s and HUD no more than 10.50/s.

## Evidence

Current production-browser screenshots are saved and visually checked:

- `docs/evidence/modular-glb-arena-kit/small-start.png`
- `docs/evidence/modular-glb-arena-kit/medium-start.png`
- `docs/evidence/modular-glb-arena-kit/large-start.png`
- `docs/evidence/modular-glb-arena-kit/medium-pointer-lock-retry.png`
