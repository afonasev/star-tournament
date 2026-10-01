# Star Tournament — Unity proving ground

Отдельный проверочный срез: 2–4 local seats, фиксированная двухэтажная арена, native CharacterController/NavMesh, существующие robot/shotgun GLB. Native FFA и Team A/B включают бой, scoring, результаты и Repeat; обычный setup поддерживает 1–4 human/AI экрана и до восьми участников. Browser baseline `8844e40` сохранён отдельно; старые replay/seed hashes здесь не воспроизводятся.

Откройте эту директорию через Unity Hub редактором **6000.3.23f1**. Точные пакеты закреплены в `Packages/manifest.json` и `packages-lock.json`. После изменения импорта используйте меню **Star Tournament → Prepare proving ground**, затем сцену `Assets/StarTournament/Scenes/ProvingGround.unity`.

Из корня репозитория используйте удобные entrypoints:

```sh
make prepare
make test-edit
make test-play
make check
make unity-editor
make unity-run
```

`make check` выполняет canonical EditMode/PlayMode tests и local Development
build; `make build` выполняет только build. Низкоуровневые эквиваленты остаются
`unity/tools.sh prepare|test-edit|test-play|build|editor|run`.

Для быстрой итерации можно выбрать fixture, метод или regex Unity Test Framework:

```sh
unity/tools.sh test-edit-filter 'DesignLabHistoryTests'
unity/tools.sh test-play-filter 'NativeBotBehaviorTests.NaturalDefaultEight'
```

Аргумент обязателен; несколько фильтров разделяются `;` внутри одного quoted
аргумента. Каждый запуск создаёт собственный `.local/unity-evidence/focused-<platform>.*`
с XML, log и `scope.txt` (filter, revision и dirty state). Это только focused check:
integration gate выбирается по [qa-scope.md](../.agents/references/qa-scope.md):
для UI-only — `make check-ui` + tests экрана и native Player QA; для gameplay/
общих зависимостей и неясного влияния — полный `make check`. Проверяйте число реально
выполненных тестов в XML: фильтр без совпадений ничего не проверяет.

Не запускайте два Unity-процесса для одного проекта одновременно. `UNITY_EDITOR` переопределяет путь редактора. Логи/XML сохраняются в `.local/unity-evidence/`; Mac Player — `unity/Builds/StarTournamentProvingGround.app`. Сборка Development, не production installer и не notarized release. Deploy/distribution command отсутствует намеренно.

Для обычного запуска выберите 1–4 места кнопками «Экранов − / Экраны +», выберите «Человек» или «AI» и назначьте устройство только человеку: `Space` присоединяет единственную keyboard/mouse пару, `Start` — конкретный gamepad. Поддержаны уникальные gamepads либо одна keyboard/mouse пара с остальными gamepads. AI-места не требуют устройств, включая состав целиком из AI. Escape и Start доступны оператору для паузы даже без назначения игрового устройства. После заполнения human slots кнопка старта получает UI focus. WASD/LS — движение, мышь/RS — взгляд, Space/A — прыжок, mouse button/RT — выстрел, Tab/View — список, Esc/Start — общая пауза. LB свободен. Disconnect/focus loss останавливает gameplay; reconnect не запускает игру автоматически. Поменять назначение можно через setup.

Пункт «Диагностика: N камеры» не требует устройств и явно помечен: он показывает неподвижных участников и не подтверждает physical acceptance. Для ограниченного автоматического измерения запустите бинарный файл Player с `-diagnostic -probeEvidence <absolute-output-directory>`. Только этот диагностический запуск разрешает выполнение в фоне; отчёт отдельно фиксирует сохранение focus. После 3 секунд прогрева (кадр пересечения границы исключён целиком) и 10 секунд записи он сохранит `report.json`, `player.png` и завершится. `-probeCameras 1|2|4` меняет только число камер при той же нагрузке четырёх неподвижных капсул. После выхода в меню CLI override камер сбрасывается; новый diagnostic соответствует выбранному составу. Без `-diagnostic` сохраняется setup screenshot. Для разрешения используйте стандартные Player аргументы `-screen-width`/`-screen-height`, проверяя фактический output в отчёте.

Профиль `unity-proving-ground-v1` редактируется в Inspector корневого `ProvingGround` через единый descriptor registry; после правок перезапустите Play. `Prepare` создаёт исходную сцену заново с default профилем. Runtime Balance Lab, immutable user revisions и replay находятся вне этого change.

ARENA-2 использует отдельный canonical profile `unity-arena-families-v1@1`: `arena.family` явно выбирает 0 `wide-hall-circuit-v1`, 1 `perimeter-gallery-v1`, 2 `split-balconies-v1`, 3 `stacked-combat-v1` или 4 `lower-basement-v1`. Development diagnostic принимает тот же explicit selector через `-arenaFamily 0..4`; фактические `arenaProfile`, `arenaFamily` и `arenaIdentity` записываются в `report.json`. Seed не выбирает family.

Обязательная приёмка остаётся отдельной: реальные четыре устройства, gamepad-only start/pause/resume, отключение/повторное подключение, TV и целевое железо. Минимум 60 FPS при четырёх игроках и Full HD–4K — цель; короткий diagnostic на M2 Pro с неподвижными игроками не доказывает её выполнение в бою. QA muted по умолчанию.

## Native layouts review

Два игрока получают равные left/right viewport; три — три равных четверти и постоянный счёт в нижней правой; четыре — 2×2. Tab/View включает дополнительную таблицу только своего viewport, включая killcam. При уменьшении состава освобождаются удалённые устройства. Repeat сохраняет состав; setup может выбрать новый.

Development Player CLI: `-layoutReview -layoutEvidence /absolute/output` с обычными `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0` (повторить 3840×2160). Review проходит 4→2→3→4, используя synthetic gamepads через настоящий adapter, shot resolver, pause/Repeat и ускоренные пустые ticks до результата. Начальные poses — fixture; это не физический playtest и не benchmark. Окну нужен focus; весь QA muted.

Data-only `NativeMatchRoster`/`NativeMatchState` поддерживают FFA и Team A/Team B для 2–8 participant slots; это не local-seat/device count. Native setup поддерживает FFA и Team A/B для 2–4 людей. Team totals и `WinnerTeam` доступны в независимом JSON inspection DTO, не restore/replay API. Handoff: `docs/tasks/completed/22-unity-team-match-state.md`.

Команды: кнопка режима в setup, назначение P1–P4 и смена цветов. Старт требует обе непустые команды. Союзник блокирует дробь без урона; Repeat сохраняет состав/цвета. `unity-native-team-v1@1` задаёт metadata initial opponent separation. Diagnostic CLI: `-teamReview -teamEvidence /absolute/path`; отдельный stationary probe: `-diagnostic -teamsDiagnostic -probeEvidence /absolute/path`. Подробнее: handoff23 и `docs/evidence/unity-native-teams-2026-09-20/README.md`.

## Обычный setup с ботами

Правая колонка setup добавляет ботов до общего лимита восьми участников. Кнопка с именем переключает Салага → Боец → Ветеран; удалить можно любую строку. В Team A/B каждый бот получает собственную кнопку команды. Обе команды должны быть непустыми; solo требует хотя бы одного бота. Устройства назначаются только людям. Repeat сохраняет состав, сложности, цвета и frozen profiles; возврат в setup сохраняет редактируемый состав. Все тела используют существующий TrooperVisual и семь v2 clips, без root motion.

Development review обычного UI: `-botSetupReview -botSetupEvidence /absolute/output` с `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0` (повторить 3840×2160). Native Player должен иметь focus. Review использует настоящие setup Buttons, synthetic gamepad joins/actions и actual AI; synthetic devices не заменяют физическую приёмку. QA muted.

## AI на отображаемых местах

Блок «ЭКРАНЫ · ЧЕЛОВЕК / AI» переключает источник управления каждого экрана; кнопка рядом выбирает сложность AI. Можно оставить одного человека и 1/2/3 AI-экрана либо сделать все экраны AI. Отображаемые боты входят в общий лимит восьми участников; дополнительные боты не получают экранов. Для all-AI не нужно назначать игровые устройства: Escape с клавиатуры или Start геймпада открывает обычную паузу, откуда доступны продолжение, Repeat и меню. В матче с людьми Start принимается только от назначенных геймпадов. Потеря focus по-прежнему ставит матч на паузу.

Focused Development review: `-botSeatsReview -botSeatsEvidence /absolute/output` с FHD/4K аргументами выше. Он проходит mixed/all-AI 2/3/4 views через ordinary setup, снимает PNG/JSON и автоматически выходит. Клавиатура и геймпад в этом review синтетические; lifecycle fallback, если нужен, явно помечен в имени evidence. Performance-цифры диагностические. Финальную пользовательскую приёмку, physical/art/TV и target performance этот прогон не закрывает.

## Минимальный UI gate

`make check-ui` запускает layout/menu/pause smoke без natural bot matches.
`python3 tools/check_ui.py --plan` показывает точный набор без запуска Unity.
Это допустимый UI-only integration gate; build и screenshots изменённых
экранов остаются обязательными для видимой правки. Scope выбирается по diff
и фиксируется в change, не определяется расширением/именем файла.
