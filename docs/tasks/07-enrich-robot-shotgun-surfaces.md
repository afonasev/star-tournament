# 07 — Робот и дробовик: поверхность и конструкция

Статус: реализация и технический QA готовы; визуальное принятие пользователем и физический mouse-look playtest остаются открытыми.

Ветка: `codex/robot-shotgun-surface-detail`, основана на `4ca5d0e` (`codex/upgrade-robot-weapon-detail`). Worktree: `/Users/eaafonasev/.codex/worktrees/5c39/star-tournament`. Использован изолированный worktree, выделенный этой задаче; main не редактировался. OpenSpec: `enrich-robot-shotgun-surfaces` (без архивации).

## Результат

- Шесть GLB получили UV и три встроенные детерминированные PNG-фактуры 128×128: покрытие с сервисной разметкой, brushed metal и ребристая резина. Отдельные material factors отделяют металлические узлы от резиновых рукоятей; identity не получает wall map.
- Фаски, открытые суставы, pectoral/shoulder/knee plates, радиаторы, крепёж, боковые service ports, прицельная планка и охлаждающие рёбра. Исправлены ориентация тороидальных колец и GLB weapon mount относительно рук.
- LOD0 robot: 104 → 169 meshes, 7760 → 14156 triangles; world shotgun: 51 → 102 meshes, 5312 → 8276 triangles; viewmodel: 89 → 140 meshes, 7752 → 9564 triangles. Это реальный рост деталей, не обещание кратного увеличения всех метрик.
- Cache освобождает embedded textures один раз; материалы экземпляров сохраняют authored maps. Симуляция, профиль, RNG, collision и combat не изменены.

## Проверка

- `npm run typecheck` — pass.
- `npm test -- src/render src/combat/shotgun.test.ts` — 6 files, 34 tests pass, включая UV/embed audit и сохранение/освобождение shared textures.
- `npm run build` — pass; остаётся Vite warning о JS chunks >500 kB.
- `openspec validate enrich-robot-shotgun-surfaces --strict` — pass.
- `git diff --check` — pass.
- Dev URL: `http://127.0.0.1:5188/`, HTTP 200, сервер именно этого worktree.
- Muted in-app Browser: меню → medium seed 1234; стандартный performance benchmark (8 фаз) — PASS; движение, jump/combat phases; ammo 20 → 17, robot-right health 100 → 77.5. После «Повторить матч» восстановились tick 0, ammo 20, health 100 и тот же seed; повторный runtime стартовал.
- Профили: `prototype-v1@5`, `presentation-balanced-v1`, Balanced; viewport 1280×720, backing buffer 1920×1080. Полный отчёт: `docs/evidence/enrich-robot-shotgun-surfaces/browser-performance.json`.
- Все восемь PNG проверены по сигнатуре и визуально. Browser screenshot API выдал JPEG bytes; они перекодированы в PNG без редактирования изображения.

## Ограничения

- Две попытки обычного входа не получили pointer lock в in-app Browser. Физические mouse look/WASD не подтверждены; benchmark использует существующий scripted input adapter и не заменяет такую проверку.
- Макет не приложен к этой задаче: точное сходство не заявлено. В первом лице компактный исходный ракурс преимущественно показывает заднюю часть оружия; camera/pose redesign не выполнялся.
- Пик всей сцены — 1767 draw calls. Около 60 submissions/s достигнуто на текущем Mac; слабые GPU, split-screen и длительный thermal soak не проверялись.
- PNG-фактуры небольшие и процедурные, без normal/ORM и GPU-compression; суммарный верхний предел 18 loaded maps ×128×128 RGBA с mipmaps ≈1.5 MiB. Это локальное расширение существующих GLB, не новый texture service.
- Inspector — QA крупный план с отдельным studio lighting, не игровой viewport. Игра показана отдельно на `match-seed-1234.png`. Раннее предупреждение duplicate Three в инспекторе устранено унификацией import; ошибок загрузки/рендера не наблюдалось.
- Merge, OpenSpec archive и deploy по поручению не выполнялись.

## Evidence

`docs/evidence/enrich-robot-shotgun-surfaces/`: robot-front-lod0.png, robot-rear-lod0.png, robot-lod1.png, shotgun-lod0.png, shotgun-lod1.png, viewmodel-lod0.png, viewmodel-lod1.png, match-seed-1234.png.

Локальный интерактивный осмотр: `http://127.0.0.1:5188/docs/evidence/enrich-robot-shotgun-surfaces/inspect.html`. Галерея исходных PNG: `gallery.html` рядом с инспектором.
