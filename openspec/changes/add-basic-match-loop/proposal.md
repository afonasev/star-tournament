## Why

Текущий combat foundation доказывает движение и стрельбу, но не образует законченный матч: у него нет roster, режима, таймера, scoring, паузы и результатов. Следующий вертикальный срез должен превратить существующий single-seat runtime в воспроизводимую игровую сессию, не выдавая неподвижных проверочных противников за готовых ботов и не расширяя scope до gamepad, split-screen или online.

Затронуты `docs/GAME_SPEC.md` §2, §4–§8 и §10–§11.

## What Changes

- Добавляется детерминированная конфигурация матча: один управляемый keyboard/mouse участник, настраиваемые неподвижные mannequin-участники без AI, FFA либо две команды, цвет, длительность 1–30 минут и опциональная цель по очкам.
- Mannequins переходят из одноразовых targets в неподвижных участников проверочного match slice: получают урон, умирают, проходят profile-defined killcam/respawn lifecycle и возвращаются без движения или атак.
- Симуляция становится единственным authority для фазы матча, оставшегося времени, overtime, roster, damage ledger, kills, assists, deaths, нанесённого/полученного урона, kill-chain scoring, team totals и результата.
- Командный лимит применяется к сумме очков команды. События одного tick учитываются атомарно; равенство после лимита либо времени запускает overtime до первого tick с единоличным лидером.
- В `prototype-v1` добавляется полное descriptor coverage цели по очкам: default 3000, minimum 1000, maximum 20 000, step 100; сама цель выключена в новой конфигурации матча по умолчанию.
- Добавляются DOM surfaces: главное/предматчевое меню, таймер HUD, live scoreboard при удержании `Tab`, pause menu и финальная таблица с повтором того же матча либо выходом в меню.
- Повтор матча создаёт новый initial snapshot из той же immutable match configuration и profile identity; выход в меню завершает runtime и не сохраняет скрытый gameplay state.
- Проверка включает deterministic/replay/component coverage, production build, strict OpenSpec, trigger-matrix performance gate, in-app Browser playtest и актуальные screenshots каждого нового состояния.
- Gamepad, split-screen, AI/bots, процедурные арены, online mode, Game Design Lab UI и полноценный audio pipeline не входят в change. Settings surface ограничивается уже применимым fullscreen control и явным обозначением отсутствующих audio consumers; новые сетевые или device contracts не создаются.

## Capabilities

### New Capabilities

- `match-session-lifecycle`: конфигурация, roster, participant lifecycle, статистика, scoring, timer, overtime и детерминированный результат FFA/двух команд.
- `match-loop-ui`: предматчевый flow, timer HUD, hold-to-view live scoreboard, pause и results/replay/menu DOM flow для одного viewport.

### Modified Capabilities

- `browser-runtime-foundation`: browser runtime получает явные menu/running/paused/overtime/finished transitions без ticks или лишней presentation work вне активного матча.
- `double-barrel-shotgun-combat`: combat damage направляется участникам матча и создаёт детерминированные damage/death/statistics events вместо permanently inactive fixture targets.
- `first-person-player-control`: keyboard adapter добавляет отдельные UI actions для удержания scoreboard и паузы, сохраняя pointer-lock/focus boundary.
- `game-design-profile-core`: match-loop числовые параметры и границы цели по очкам получают обязательные descriptors и validation.

## Impact

- Будут изменены versioned `GameDesignProfile`, playable snapshot/replay и combat scenario identity; старые snapshots/replays должны отклоняться как несовместимые до первого tick. Gameplay action schema остаётся совместимой, а `Tab`/`Escape` живут в отдельном локальном UI-action contract.
- Основные области кода: `src/profiles`, `src/input`, `src/simulation`, `src/combat`, `src/scenario`, `src/runtime`, `src/ui`, `src/styles.css`, performance driver и тесты.
- Renderer остаётся presentation-only; stationary mannequin transforms выводятся из snapshot, а DOM не вычисляет scores или match result.
- Новые runtime dependencies и внешние сервисы не требуются.
- Общая семантика team spawn, полностью занятого набора spawn anchors, будущих активных bots и нескольких local seats остаётся открытой. В этом slice неподвижный mannequin возвращается в свой versioned fixture anchor; это не объявляется универсальным spawn contract.
