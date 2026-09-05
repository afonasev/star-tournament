# 08 — Полная анимация light-sport-robot

Статус: завершено, интегрировано в main и архивировано. Деплой не выполнен. Результаты и dev URL ниже относятся к исходной проверке.

Ветка: `codex/animate-light-sport-robot`, от `62d2a84`.

## Принятый объём

- Сохранить принятые robot, weapon и first-person arms в нейтральной позе.
- Ввести LOD-consistent жёсткую суставную иерархию без skin deformation.
- Реализовать renderer-only idle, walk/run/strafe, jump, aim, firing recoil, hit reaction и destruction.
- Добавить отдельный muted animation-review sandbox для просмотра со стороны; он не служит gameplay или physical-input evidence.
- Не менять deterministic simulation, collision, hit volumes, replay/state hash, camera и принятый first-person recoil.

## Ожидаемая проверка

- Unit/source audits для hierarchy, LOD parity и state/event mapping.
- Typecheck, relevant tests, production build, strict OpenSpec validation и diff check.
- Muted in-app Browser: sandbox и игровой renderer; PNG каждого изменённого состояния, сохранённые и визуально проверенные.

## Результат

- Создана `light-sport-robot-animation-v1` rigid hierarchy обоих LOD: 353 и 165 node соответственно, с required pelvis/spine/head/arm/leg/weapon-mount pivots и без skin/animation clips.
- Добавлен renderer-owned controller с idle, walk/run/strafe, jump, aim/fire, hit и destruction poses. Он получает только presentation time/state и меняет только named joint rotations; accepted first-person recoil остаётся отдельным.
- `?animationReview` открывает muted third-person sandbox с явными controls для всех states, LOD0/LOD1 и automatic full review. Сохранены и визуально проверены canvas PNG для idle, walk/run/strafe, jump phases, aim/fire, hit, destruction и LOD1, а также GIF полного LOD0-прогона в `docs/evidence/animate-light-sport-robot/`.
- In-app Browser подтвердил URL `http://127.0.0.1:5192/?animationReview`, оба LOD и инициализацию игрового renderer после старта локального матча. Browser не выдал pointer-lock, поэтому это не доказательство physical mouse-look; audio был выключен.
- `npm run typecheck`, focused Vitest (4 files/35 tests), `npm run build`, `openspec validate animate-light-sport-robot --strict` и `git diff --check` прошли. Vite предупреждает о chunks >500 kB.
- После исходной проверки выполнены merge и archive (`openspec/changes/archive/2026-09-05-animate-light-sport-robot/`). Деплой не выполнялся.
