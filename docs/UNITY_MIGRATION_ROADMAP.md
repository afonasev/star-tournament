# Star Tournament — roadmap перехода на Unity

Уточнение пользователя 2026-09-20: проверенный отдельный срез — `add-unity-bot-controlled-seats` (handoff 29: 81/81 EditMode, 63/63 PlayMode, Mac build, focused/muted mixed/all-AI 2/3/4 FHD/4K), ordinary human/AI на 1–4 views, включая all-AI и один human + 1/2/3 AI, максимум восемь участников. Replay пока отложен; поставка обсуждается отдельно. Финальную пользовательскую приёмку пользователь проведёт после реализации; каждый срез сохраняет tests/build и muted native Player QA. Physical/art/reference-performance acceptance этим не закрываются.


Дата: 2026-09-19. Исходный browser commit: `8844e40` (`v0.0.9`).
Статус: направление перехода и начало работы разрешены пользователем; технические рекомендации ниже не равнозначны утверждению всех открытых продуктовых решений.

2026-09-20 пользователь поручил полный перенос до проверенного результата. Актуальный requirement/evidence backlog — `docs/UNITY_MIGRATION_MATRIX.md`. Навигационный срез `add-unity-bot-navigation` продолжает базу `6f8ed15`; его handoff — `docs/tasks/completed/25-unity-bot-navigation.md`. Внутренние foundations не завершают этап 2 или полную цель. Следующие sidebar-задачи автоматически не создаются; разрешены встроенные subagents и отдельные changes/worktrees/commits. Навигация зафиксирована `45e7f27`; проверенный срез `add-unity-participant-roster` подтверждает отдельные participant/local-view identities и восемь native тел, сохраняя planner/setup/evaluation открытыми.

## 1. Цель и приоритеты

Перевести игру на Unity, используя готовые системы движка и расширения там, где они лучше нашего решения. Сохранять игровые требования, контент и проверенные правила; существующий код не получает приоритета только потому, что уже написан. Не начинать с механического перевода TypeScript в C#.

Главный сценарий: скоростной стилизованный first-person arena shooter для игры на телевизоре, до четырёх local seats и до восьми участников суммарно. Сохранить FFA/teams, дробовик, многоэтажные процедурные арены, ботов, Balance Lab и путь к онлайн-игре.

Переход обоснован возможностями развития графики, анимации, авторинга и инструментов. Достижение предела browser-графики пока не доказано измерениями. Производительность Unity также не считается доказанной до измерения Player build на целевом железе.

`docs/GAME_SPEC.md` остаётся канонической спецификацией. Новые решения сначала согласовать и внести туда и в журнал, затем реализовывать через отдельные OpenSpec changes. Сохранение этого roadmap не отменяет текущих требований.

## 2. Техническая основа первого полигона

- Для первого полигона утверждены установленная Unity 6000.3.23f1 и URP 17.3.0; точные resolved packages и evidence см. `UNITY_STAGE0.md` и `unity/Packages/`.
- URP как рекомендуемый pipeline для стилизованной графики и четырёх viewport. HDRP рассматривать только при конкретной визуальной необходимости и подтверждённом бюджете.
- Windows/macOS и компьютер, подключённый к телевизору, — рабочая гипотеза, пока не утверждённая матрица платформ. Browser build не является целью этого переноса по умолчанию.
- GameObjects/Prefabs, C# и небольшие явные адаптеры. Не вводить ECS/DOTS без измеренной необходимости.
- Gameplay state и правила отделены от renderer, UI, устройств и камер. `LocalSeat` связывает человека, устройства и viewport; не равен network client или participant entity.
- Все новые параметры баланса, движения, камер и читаемости входят в именованные профили с единым descriptor registry: путь, группа, подпись, описание, единица, min/max/step.

## 3. Решения по подсистемам

| Подсистема | Предпочтительное направление | Что сохраняем |
| --- | --- | --- |
| Рендеринг | Полная замена Three.js на URP, Materials, Shader Graph, штатный свет, тени и decals | Clean-future-sport, читаемость силуэта и команд, quality profiles |
| Ввод | Input System, PlayerInput/PlayerInputManager | Action contract, LocalSeat, назначение устройств, join/reconnect |
| Split-screen | Unity cameras и per-seat viewport | 1/2/3/4 layouts, при трёх игроках постоянный счёт в четвёртой ячейке, отдельные HUD |
| Движение | Сравнить CharacterController и готовый kinematic motor | Темп, strafe, ускорение, air control, прыжок |
| Коллизии | Оценить Unity-native queries/motor вместо Rapier; отказ от строгого детерминизма утверждён | Различие movement/projectile queries, твёрдые живые участники, лестницы и headroom |
| Навигация | Сначала AI Navigation/NavMesh, A* как альтернативный кандидат | Физически допустимые маршруты и явные связи этажей |
| Боты | Портировать решения высокого уровня; заменить геометрическую навигацию при преимуществе готовой | Восприятие, память, выбор целей, сложность без бонусов к характеристикам |
| Анимация | Animator, Animation Rigging, применимые готовые клипы | Состояния робота и связь с событиями; animation не управляет уроном |
| Камеры | Прямая FPS camera; Cinemachine для killcam и переходов | Прицеливание без задержки, per-seat ownership, правила смерти |
| Эффекты | Particle System/Shader Graph; VFX Graph при обоснованной необходимости | Стиль выстрела, читаемость попаданий и бюджеты |
| UI | Переписать; uGUI — первый кандидат для per-seat HUD и controller UI | Сценарии setup, pause, scoreboard, results, Repeat |
| Balance Lab | Unity UI/editor над переносимой схемой; ScriptableObject может быть оболочкой авторинга | Immutable revisions, releaseRef, metadata, импорт/экспорт и content identity |
| Аудио | AudioSource/AudioMixer как основа | Отдельно определить общий микс для split-screen, а не умножать звук на число камер |
| Матч | C# gameplay layer | FFA/teams, scoring, damage, ammo, death/respawn |

## 4. Готовые расширения: сравнить до переписывания

| Кандидат | Потенциальная замена | Проверка применимости |
| --- | --- | --- |
| Easy Character Movement 2 | Собственная обработка склонов, ступеней, контактов | Strafe, air control, stairs, низкий потолок, живые капсулы, управление из simulation tick |
| Opsive Ultimate Character Controller | Более полный FPS stack с camera/animation/split-screen | Стоимость интеграции против небольшого motor, отключаемые правила, ownership состояния |
| Unity AI Navigation | Собственный геометрический pathfinding | Runtime bake, несколько этажей, ramps/stairs, отсутствие ложных XZ-связей |
| A* Pathfinding Project | Альтернатива NavMesh при выявленном ограничении | Контроль графа и переходов, runtime updates, стоимость CPU и сопровождения |
| Dungeon Architect | Собственный procedural geometry generator | Циклы, независимые переходы, многоэтажность, ширина проходов, экспорт результата в проверяемые данные |
| Odin Inspector | Часть editor tooling | Реальное сокращение работы; не заменяет runtime Balance Lab и её schema |

Особенно проверить модульные авторские комнаты и переходы вместо дальнейшего усложнения procedural geometry. Dungeon Architect поддерживает модульную сборку и графы нескольких этажей, но arena fairness, spawn safety и проверки прострелов остаются нашими.

Покупки отдельно согласовывать. До выбора проверить точные версии, лицензию, поддержку платформ, source access, возможность автоматических/headless проверок и цену интеграции. Документация подтверждает наличие возможностей, но не совместимость с нашей игрой. Если готовый пакет проходит наши сценарии лучше, заменить собственный механизм.

## 5. Повторное использование текущих наработок

- Перенести игровые правила и профильные данные: дробовик, аналитические зоны попаданий, respawn, режимы, scoring, AI difficulty, descriptor metadata.
- Использовать GLB роботов, оружия, архитектуры, текстуры, LOD и semantic mount points. glTFast позволяет импортировать GLB в native Unity prefabs. Проверить handedness/orientation, метры, pivot, normal/ORM mapping, материалы и shader stripping в Player build.
- Native integration использует утверждённый 2026-09-19 trooper v2: существующий skinned rig, same-rig baked clips и производные first-person руки. Generic Animator/manual Playables сохраняет gameplay authority; Humanoid Avatar и чужой retargeting не требуются этим срезом и отдельно не заявляются проверенными. Прежний rigid robot остаётся historical baseline.
- Сохранить тестовые сценарии, проблемные seeds, требования к spawn и физической проходимости. Vitest-код не переносится непосредственно; сценарии становятся Unity/C# проверками.
- Сохранить semantic IDs, конфигурацию, события и отделение presentation от gameplay.
- Three.js renderer, React DOM, browser input, pointer-lock lifecycle и browser asset loader заменить native-реализациями. Не встраивать браузер ради их сохранения.
- Старые форматы replay/snapshot не обещать совместимыми с новым runtime без отдельного доказательства.

## 6. Утверждённый отказ от строгого детерминизма

2026-09-19 пользователь явно подтвердил: «готов отказаться от детерминизма». Для Unity выбран путь native physics/motor/navigation без обязательной побитовой воспроизводимости. Не требовать повторного подтверждения этой развилки. Сначала сравнивать готовые системы по игровому поведению, качеству, производительности и стоимости сопровождения.

Сериализуемое gameplay state, профили и отделение правил от presentation сохраняются. Fixed timestep полезен для организации runtime, но не гарантирует детерминизма. PhysX не гарантирует одинаковый результат между платформами/сборками.

Browser baseline использовал deterministic Rapier 0.20.0 и collision-contract-v2. Его historical replay и hashes остаются эталоном исходной версии, а не обязательством нового runtime. Legacy browser implementation удалена из текущего дерева 2026-09-21; exact code доступен только через Git history и сохранённые evidence. Stage 0 сверил GAME_SPEC с тогдашним кодом: collision v2, match configuration v5 с legacy v4; snapshot v4 и replay v5.

Пользователь выбрал Unity replay по записанным состояниям, но 2026-09-20 отложил его реализацию. Его детальная схема, частота записи, события/seek/restore и необходимость historical compatibility ещё открыты. Отказ от детерминизма не утверждает автоматически server-authoritative, lockstep либо иной сетевой контракт. Пока не подключать transport/сервисы без отдельного выбора модели.

## 7. Этапы и критерии завершения

### Этап 0. Требования и эталон

- Утверждены Full HD–4K output и минимум 60 FPS при четырёх игроках; уточнить reference PC/TV, quality/internal scale и frame-time методику. Первый срез без покупок; бюджет расширений позднее.
- Replay отложен решением 2026-09-20; его детализация не блокирует текущие срезы.
- Снять точный source commit/profile/seed/scenario baseline; сохранить Git history и historical evidence browser-версии как эталон.
- Согласовать устаревшие пункты GAME_SPEC, затем обновить утверждённые решения и оформить первый ограниченный OpenSpec change.
- Прочитать актуальное окружение Unity; ничего не предполагать по историческим версиям соседнего unity-rts.

Выход: согласованный первый срез, критерии оценки, список открытых решений без неявных defaults.

### Этап 1. Проверочный игровой фрагмент

- Одна многоэтажная арена, существующие robot/shotgun assets, четыре управляемых local players с отдельными устройствами, камерами и HUD (уточнение пользователя 2026-09-19).
- Сравнить CharacterController/готовый motor; проверить NavMesh на ramps/stairs/headroom и нескольких этажах.
- Измерить CPU/GPU/frame times в Player build; сравнить картинку, input latency и поведение движения.
- Исследование и подготовка независимых материалов разрешены до ответов пользователя; зависимую от открытых решений реализацию не начинать.

Выход: выбранные версии Unity/URP и пакетов, доказательства пригодности controller/navigation, решение о продолжении.

### Этап 2. Полный локальный матч

Первый внутренний срез `add-unity-combat-state`: отдельный сериализуемый lifecycle здоровья/боезапаса/cooldown/death/respawn readiness и профиль `unity-combat-state-v1`. Проверенный срез `add-unity-native-combat` подключает его к native Player: pellet queries/hit zones, damage, capsule death/respawn, killcam/HUD и выбор безопасного spawn на фиксированной арене; handoff `docs/tasks/completed/18-unity-native-combat.md` содержит актуальный статус проверки. Следующий срез `add-unity-native-match-loop` подключает scoring, atomic match completion/overtime, native standings/results и clean Repeat/setup; актуальные проверки — в `docs/tasks/completed/19-unity-native-match-loop.md`. Следующий срез `add-unity-native-seat-layouts` переносит 2–4 human seats: выбор состава, left/right и 2×2 камеры/HUD, постоянный счёт в четвёртой ячейке при трёх людях, exact-N combat roster и Repeat; handoff `docs/tasks/completed/20-unity-native-seat-layouts.md` отслеживает проверку. Layouts идут до bots, поскольку убирают фиксированную потребность в четырёх human devices. Teams/bots, восемь участников и playable solo остаются отдельными проверяемыми срезами; весь этап 2 этим не закрыт. Это порядок реализации уже утверждённых требований, не снятие функций с этапа.


Следующий внутренний срез `add-unity-team-match-state` расширяет data-only match reducer: immutable FFA/Team A/Team B roster для 2–8 participant slots, сумма личных очков команд, отдельная team winner identity, atomic target/time/overtime и независимые snapshot DTO. Внутренний срез не подключал native teams; следующий `add-unity-native-teams` переносит команды в Player. Реальные восемь участников остаются отдельной задачей. Проверки и следующий handoff — `docs/tasks/completed/22-unity-team-match-state.md`.


Native teams-срез `add-unity-native-teams` подключает FFA/Team A/B для 2–4 human seats: setup/цвета, allied pellet blocking без damage, initial/respawn, grouped standings/results и frozen Repeat. Проверки и ограничения: `docs/tasks/completed/23-unity-native-teams.md`. Bots, playable solo и восемь scene participants остаются следующими отдельными срезами; physical/TV и long 60 FPS gates не закрыты.

Внутренний prerequisite `add-unity-bot-perception` переносит ограниченные FOV/LOS-наблюдения, память и delayed allied reports с actual PhysicsScene adapter. Он не добавляет игровых ботов: navigation/actions проверены следующим внутренним срезом 25; shipping planner/support и participant/local-seat separation остаются следующими работами, затем playable solo и восемь тел. Проверки — в `docs/tasks/completed/24-unity-bot-perception.md`; этап 2 и physical/performance gates остаются открытыми.

- C# gameplay: damage, death, respawn, FFA/teams, восемь участников, bots, results и Repeat.
- 1–4 local devices, layouts, HUD, scoreboard, pause/focus/reconnect.
- Контроль сериализуемого gameplay state и clear ownership Unity adapters.

Выход: законченный матч от меню до результатов с реальными устройствами.

### Этап 3. Производство арен

- Сравнить перенос текущего генератора с Dungeon Architect/модульным авторингом.
- Подключить общую проверяемую модель collision/navigation/spawn и fairness validators.
- Не допустить второго независимого источника геометрии между Unity scene и gameplay definition.

Выход: разнообразные арены с подтверждённой физической проходимостью и честностью.

### Этап 4. Визуальное развитие

- URP materials/light, Animator/Rigging, camera transitions, weapon/impact effects, подходящие готовые ассеты.
- Отдельно выбрать освещение runtime-generated layouts: baked lighting не считать автоматически применимым к произвольной сборке комнат.
- Ограничивать lights/shadows/particles по общему бюджету четырёх камер; проверить 1/2/3/4 seats.

Выход: визуально принятый результат и подтверждённое время кадра на целевой машине.

### Этап 5. Инструменты и баланс

- Runtime Balance Lab, profile revisions, release catalog, импорт/экспорт и editor diagnostics.
- Ускоренный прогон ботов на том же gameplay runtime; сохранение проблемных состояний/записей по новому replay contract.
- Один descriptor registry управляет UI и диапазонной валидацией.

Выход: изменение баланса без правки кода и воспроизводимые проверки в рамках выбранного контракта.

### Этап 6. Поставка и переключение основной версии

Поставка обсуждается отдельно после нового решения; этот раздел не разрешает публикацию. Финальная пользовательская приёмка проводится пользователем после завершения реализации; автоматические проверки каждого среза продолжаются.

- Сборки Windows/macOS согласно согласованной матрице, выпуск и обновления, smoke установленного приложения.
- Реальные gamepads/TV, reconnect, focus, readable HUD, длинный матч, audio-specific проверка отдельно от muted QA.
- Обновить browser-specific workflow на native Player acceptance перед применением к Unity; не объявлять browser smoke доказательством native input.
- Не восстанавливать browser runtime ради Unity acceptance; сохранять его Git history и historical evidence, не ведя две полноценные реализации новых фич.

Выход: Unity становится основной версией после подтверждённой приёмки, а не только успешной сборки.

## 8. Закрытый browser backlog

2026-09-19 пользователь распорядился закрыть текущие незавершённые browser-задачи: на Unity реализация и тестирование создаются заново. Все оставшиеся работы и acceptance gates старого runtime сняты с дальнейшего выполнения; это не успешная приёмка. Реестр: `docs/BROWSER_BACKLOG_CLOSURE.md`.

`add-local-split-screen-controls` архивируется без spec sync: исторические 6/9 остаются 6/9, проверки 3.1–3.3 не объявляются выполненными. Четыре оставшихся handoff переносятся из активного списка с явным статусом отмены остатка работ. Неинтегрированная ветка robot shadows не является кандидатом на дальнейший merge/deploy.

Старые код и тесты доступны через Git history как справочный материал; обязательства буквально переносить реализацию или закрывать старую browser-приёмку нет. Продуктовые требования и пригодные ассеты сохраняют ценность. Native input, графика, physics и performance получают новые Unity-проверки.

Оценку сроков делать после этапа 1: ключевые неопределённости — motor/physics contract, tooling генератора и native split-screen. Не обещать процент повторного использования по количеству строк.

## 9. Организация следующей сессии

- Основная новая задача: Astra (`gpt-6-astra`), medium — явный выбор пользователя.
- Пользователь разрешил сабсессии с другими моделями для экономии контекста/токенов. Использовать встроенных subagents для ограниченных независимых подзадач; не создавать дополнительные sidebar tasks без отдельного запроса.
- GPT-6 Sol/medium — исследования и реализация; GPT-6 Luna — понятные ограниченные задачи; Spark — совсем механическая инвентаризация и правки при доступности модели; GPT-6 Astra — архитектура, сложная диагностика или прямой запрос. Следовать `.agents/references/model-routing.md` и явному выбору пользователя.
- Архитектурные развилки: read-only astra_architect согласно routing; передавать узкий вопрос и пути, возвращать компактный decision memo, не копировать всю историю.
- Для model overrides передавать `fork_turns=none` либо небольшое число, с явными требованиями и исходными файлами. Работникам назначать непересекающееся владение файлами, сообщать о параллельной работе и запрете отката чужих изменений.
- Основной агент проверяет результаты, управляет решениями и последовательно выполняет интеграцию. Не делегировать ради делегирования.
- Начать с этапа 0 и независимой подготовки этапа 1. Задавать небольшие группы существенных вопросов; не повторять уже утверждённый выбор Unity и приоритет готовых систем.
- Каждая отдельная implementation change — отдельная ветка/worktree, проверки и коммит. Не реализовывать весь roadmap одним монолитным change.

## 10. Источники исследования

Документация проверена 2026-09-19; точные пакеты ещё не устанавливались и не испытывались. Страницы latest/новых Unity используются как подтверждение возможностей, не как выбор версии пакета для 6.3.

- [Unity 6 support](https://unity.com/releases/unity-6/support)
- [Unity 6.3 render pipeline comparison](https://docs.unity3d.com/6000.3/Documentation/Manual/render-pipelines-feature-comparison.html)
- [Input System PlayerInputManager](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/PlayerInputManager.html)
- [Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html)
- [glTFast editor import](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportEditor.md)
- [Easy Character Movement 2](https://oscar-gracian.gitbook.io/easy-character-movement-2/user-manual/general/components)
- [Opsive Ultimate Character Controller](https://opsive.com/assets/ultimate-character-controller/)
- [AI Navigation](https://docs.unity.com/en-us/engine/6000.7/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation)
- [A* RecastGraph](https://arongranberg.com/astar/documentation/stable/recastgraph.html)
- [Dungeon Architect Unity](https://dungeonarchitect.dev/unity/)
- [Odin Inspector](https://odininspector.com/tutorials/serialize-anything/features-and-limitations)
- [PhysX determinism limitations](https://nvidia-omniverse.github.io/PhysX/physx/5.8.0/docs/API.html)

Participant/local-view separation проверено в `docs/tasks/completed/26-unity-participant-roster.md`: восемь физических участников,1–4 local views, mapped actions/presentation и bounded initial allocator;66/66 EditMode,49/49 PlayMode, Mac build, FHD/4K. Это внутренний prerequisite для playable planner/setup; прежние упоминания восьми тел как будущей работы выше описывают хронологию. Полная миграция остаётся активной.

`add-unity-bot-planner` (handoff27) подключает настоящие AI actions/combat и frozen lifecycle; automated и focused muted FHD/4K Player evidence verified. Shipping setup следует отдельным change; никакого снятия evaluation/generated arena/physical/performance требований.

`add-unity-bot-controlled-seats` завершает только выбор human/AI на отображаемых местах и operator lifecycle. Evidence: `docs/evidence/unity-bot-seats-2026-09-20/README.md`; handoff `docs/tasks/completed/29-unity-bot-controlled-seats.md`. Общий TrooperVisual/семь v2 clips и bot driver сохранены. Изменение остаётся неархивированным и неинтегрированным; другие этапы не начаты.
