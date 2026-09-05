# 07 — Робот и дробовик: поверхность и конструкция

Статус: завершено. Пользователь принял визуальный результат и поручил закрыть текущие правки. Ограничения ручного mouse-look QA сохранены ниже.

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


## Продолжение equipment-surfaces-v2

Пользователь оценил первый pass как «сильно лучше» и запросил «ещё больше деталей, меньше крупных квадратов».

Реализованы polygon shells со срезанными углами и сужением, округлые плечи, многогранная броня конечностей, позвоночные сегменты, гибкие шланги, оси суставов и детали шлема/кистей. У оружия добавлены задний breech, секционные кожухи, feed lines и шарниры; задний узел виден в прежнем first-person ракурсе. Контрастная текстурная рамка вокруг каждой панели заменена мелкой сервисной разметкой.

Сравнение LOD0 с 0865ca3: robot 169 → 232 nodes, 14156 → 10516 triangles, 1.18 → 0.71 MB; world shotgun 102 → 129 nodes, 8276 → 7204 triangles; viewmodel 140 → 179 nodes, 9564 → 10172 triangles. Новые polygon shells экономнее прежних RoundedBoxGeometry. Draw calls ещё не измерены.

Typecheck, 34 renderer/combat tests, build, strict OpenSpec, diff check и finite POSITION/NORMAL/UV audit всех шести GLB — PASS. Новый browser QA и screenshots пока ожидают разблокировки Mac; старые PNG не доказывают результат v2. Следующий шаг: открыть `/docs/evidence/enrich-robot-shotgun-surfaces-v2/inspect.html` на подтверждённом HTTP 200 dev origin `http://127.0.0.1:5188/`, осмотреть оба LOD и игровой кадр, затем коммит.


## Продолжение equipment-surfaces-v3

Утончены ширина корпуса, талия, плечи и конечности при прежней высоте. Таз получил срезанные углы; внутренние плечевые шарниры уменьшены, чтобы не прорезать броню. First-person руки пересобраны: правый хват рукояти, левый хват цевья, по четыре трёхсегментных пальца и большой палец в обоих LOD, металлические запястья и предплечья, выходящие за нижнюю границу камеры. Камера, FOV, recoil и simulation остаются прежними.

Финальные проверки: 36 tests (7 files), build/typecheck, strict OpenSpec — PASS. Новый spatial regression проверяет пересечение границ предплечья, запястья, ладони и цепочек пальцев в обоих LOD; это консервативная проверка разрывов, не доказательство точного поверхностного контакта.

Muted in-app Browser: medium seed1234, prototype-v1@5, Balanced, viewport845×993; benchmark PASS по восьми фазам, повторный матч восстанавливает ammo20/HP100/tick0. Ручной pointer-lock playtest остаётся неподтверждённым. Inspector проверен с тем же FOV90 и transform модели в idle/recoil, обоими LOD, плюс canvas16:9 (845×475); 16:9 — отдельный QA canvas, не полноэкранный игровой viewport. PNG сохранены как настоящие PNG и осмотрены. Артефакты v3 заменяют прежнее ожидание разблокировки Mac.

Evidence: `docs/evidence/enrich-robot-shotgun-surfaces-v3/`: robot-front/rear/lod1.png, hands.png, fps-idle/recoil/lod1/wide.png, recoil-wide.png, match.png, browser-performance.json, inspect.html. Inspector использует отдельный studio lighting; match.png показывает реальную игру. Обновление остаётся в той же ветке; merge/archive/deploy не выполнялись. Визуальное принятие пользователем остаётся открытым.


## Продолжение equipment-surfaces-v4

Крупный план first-person рук получил кольцевые стыки с металлическими кромками, продольные швы, утопленные винты, вставки металла и отдельный материал манжет. Направленная металлическая фактура first-person GLB увеличена до 256×256; остальные изображения сохраняют 128×128. Основные стыки и часть крепежа сохранены в LOD1. Поза и камера не изменены.

36 tests, build/typecheck, strict OpenSpec и diff check — PASS. Muted in-app Browser benchmark: 8 фаз PASS, medium seed1234, Balanced, 1280×720; пиковые draw calls всей сцены2057, ошибок console нет. Этот показатель нельзя напрямую сравнивать с portrait-прогоном v3. Физический mouse-look этим сценарным прогоном не подтверждён. JS chunk warning >500kB остаётся. First-person GLB LOD0 вырос с615 до958kB, LOD1 с298 до409kB.

Актуальные PNG в docs/evidence/enrich-robot-shotgun-surfaces-v4/: idle.png, recoil.png, lod1.png, hands.png (studio inspector) и match.png (игровой кадр seed1234), рядом browser-performance.json. Швы и хват проверены визуально в обоих LOD и при отдаче. Визуальное принятие открыто; merge/archive/deploy не выполнялись.


## Продолжение equipment-surfaces-v5

Детализирован только робот: швы грудных, бедренных и коленных панелей, утопленные винты, металлические кромки, обоймы предплечий/голеней, вставки на голенях, швы шлема, уплотнения шеи и сервисные разъёмы спины. Металлическая текстура робота256×256. Основные швы сохранены в LOD1; стройный силуэт и simulation не меняются. Все четыре GLB оружия/first-person побайтно совпадают с41875c7.

36 tests, build/typecheck, strict OpenSpec, diff check — PASS. Muted Browser: medium seed1234, Balanced,1280×720, benchmark8 фаз PASS, пик всей сцены2265 draw calls, ошибок console нет. Дополнительные детали увеличивают стоимость отображения; слабые GPU/split-screen не проверены. Ручной mouse-look сценарным benchmark не подтверждён. Размер robot LOD0 вырос712→981kB, LOD1429→522kB.

Актуальные PNG (проверены сигнатура и вид): docs/evidence/enrich-robot-shotgun-surfaces-v5/front.png, rear.png, lod1-front.png, lod1-rear.png — studio inspector; match.png — игровой кадр seed1234. Там же browser-performance.json и inspect.html. Dev origin http://127.0.0.1:5188/. Визуальное принятие робота ожидается; руки и оружие приняты пользователем. Merge/archive/deploy не выполнялись.


## Закрытие и продолжение в новой сессии

2026-09-05: визуальная работа принята. Реализация закоммичена до6c7b2f5; OpenSpec синхронизирован в participant-weapon-presentation и архивирован в2026-09-05-enrich-robot-shotgun-surfaces. Доставка остаётся в codex/robot-shotgun-surface-detail; main merge и deploy не выполнялись в рамках этой задачи.

Следующая отдельная сессия — обсуждение и реализация анимации робота. Сейчас робот статичен: плоский набор геометрии без skin/skeleton/animation clips; renderer меняет transform целой модели и состояние уничтожения. First-person отдача и muzzle flashes уже работают.

Начать с docs/GAME_SPEC.md, scripts/generate-participant-assets.mjs, src/render/participantWeaponPresentation.ts и src/render/firstPersonRenderer.ts. Согласовать набор движений и способ суставной иерархии, затем создать отдельный OpenSpec change и worktree. Ходьба/бег/прыжок/прицеливание/реакции — кандидаты для обсуждения, не утверждённый scope. Сохранить принятый силуэт, детали робота, оружие и first-person руки. Не считать текущие цельные GLB уже пригодным скелетным rig.
