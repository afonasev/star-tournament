# Star Tournament — Unity proving ground

Отдельный проверочный срез: 2–4 local seats, фиксированная двухэтажная арена, native CharacterController/NavMesh, существующие robot/shotgun GLB. Native FFA включает бой, scoring, результаты и Repeat; teams/bots, восемь участников и solo ещё не подключены. Browser baseline `8844e40` сохранён отдельно; старые replay/seed hashes здесь не воспроизводятся.

Откройте эту директорию через Unity Hub редактором **6000.3.23f1**. Точные пакеты закреплены в `Packages/manifest.json` и `packages-lock.json`. После изменения импорта используйте меню **Star Tournament → Prepare proving ground**, затем сцену `Assets/StarTournament/Scenes/ProvingGround.unity`.

Из корня репозитория:

```sh
unity/tools.sh prepare
unity/tools.sh test-edit
unity/tools.sh test-play
unity/tools.sh build
```

Не запускайте два Unity-процесса для одного проекта одновременно. `UNITY_EDITOR` переопределяет путь редактора. Логи/XML сохраняются в `.local/unity-evidence/`; Mac Player — `unity/Builds/StarTournamentProvingGround.app`. Сборка Development, не production installer и не notarized release.

Для обычного запуска выберите 2–4 места кнопками «Игроки −/+» и назначьте каждому устройство: `Space` присоединяет единственную keyboard/mouse пару, `Start` — конкретный gamepad. Поддержаны уникальные gamepads либо одна keyboard/mouse пара с остальными gamepads. После заполнения slots кнопка старта получает UI focus. WASD/LS — движение, мышь/RS — взгляд, Space/A — прыжок, mouse button/RT — выстрел, Tab/View — список, Esc/Start — общая пауза. LB свободен. Disconnect/focus loss останавливает gameplay; reconnect не запускает игру автоматически. Поменять назначение можно через setup.

Пункт «Диагностика: N камеры» не требует устройств и явно помечен: он показывает неподвижных участников и не подтверждает physical acceptance. Для ограниченного автоматического измерения запустите бинарный файл Player с `-diagnostic -probeEvidence <absolute-output-directory>`. Только этот диагностический запуск разрешает выполнение в фоне; отчёт отдельно фиксирует сохранение focus. После 3 секунд прогрева (кадр пересечения границы исключён целиком) и 10 секунд записи он сохранит `report.json`, `player.png` и завершится. `-probeCameras 1|2|4` меняет только число камер при той же нагрузке четырёх неподвижных капсул. После выхода в меню CLI override камер сбрасывается; новый diagnostic соответствует выбранному составу. Без `-diagnostic` сохраняется setup screenshot. Для разрешения используйте стандартные Player аргументы `-screen-width`/`-screen-height`, проверяя фактический output в отчёте.

Профиль `unity-proving-ground-v1` редактируется в Inspector корневого `ProvingGround` через единый descriptor registry; после правок перезапустите Play. `Prepare` создаёт исходную сцену заново с default профилем. Runtime Balance Lab, immutable user revisions и replay находятся вне этого change.

Обязательная приёмка остаётся отдельной: реальные четыре устройства, gamepad-only start/pause/resume, отключение/повторное подключение, TV и целевое железо. Минимум 60 FPS при четырёх игроках и Full HD–4K — цель; короткий diagnostic на M2 Pro с неподвижными игроками не доказывает её выполнение в бою. QA muted по умолчанию.

## Native layouts review

Два игрока получают равные left/right viewport; три — три равных четверти и постоянный счёт в нижней правой; четыре — 2×2. Tab/View включает дополнительную таблицу только своего viewport, включая killcam. При уменьшении состава освобождаются удалённые устройства. Repeat сохраняет состав; setup может выбрать новый.

Development Player CLI: `-layoutReview -layoutEvidence /absolute/output` с обычными `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0` (повторить 3840×2160). Review проходит 4→2→3→4, используя synthetic gamepads через настоящий adapter, shot resolver, pause/Repeat и ускоренные пустые ticks до результата. Начальные poses — fixture; это не физический playtest и не benchmark. Окну нужен focus; весь QA muted.
