# Unity Stage 0 — локальная инвентаризация

Дата проверки: **2026-09-19**. Контур связан с change `add-unity-four-player-proving-ground`; это инвентаризация, а не подтверждение переноса или производительности.

## Среда

Локально установлен Unity `6000.3.23f1` по пути `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app` (проверено через `Contents/Info.plist`). Найден только `MacStandaloneSupport`; Android, iOS, Windows, Linux и WebGL support-модули не обнаружены. Машина: MacBook Pro `Mac14,10`, Apple M2 Pro, 12 cores, 32 GB RAM, macOS `14.6.1 (23G93)`. Серийные и пользовательские идентификаторы намеренно не фиксируются.

Пакеты и модули, подтверждённые локальными файлами:

| Компонент | Наблюдение |
|---|---|
| URP | `com.unity.render-pipelines.universal` `17.3.0`; core `17.3.0`; shadergraph `17.3.0` |
| Tests | `com.unity.test-framework` `1.6.0`; NUnit `2.0.5`; UI Test Framework `6.3.0` |
| AI | встроенный `com.unity.modules.ai` `1.0.0`; отдельный `com.unity.ai.navigation` локально не найден |
| Input | движковый Input module есть; Input System DLL присутствует в кэше шаблонов, но установленный project package не подтверждён |
| glTF | `com.unity.cloud.gltfast` локально не установлен; реестр редактора знает minimum/registry `6.14.1` |

Основные источники: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/modules.asset`, `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json`, каталог `.../Resources/PackageManager/BuiltInPackages/` и кэш шаблона `.../ProjectTemplates/libcache/com.unity.template.3d-cross-platform-17.0.14/`. Контекстный fetch документации не сработал; прямое чтение registry metadata сработало и показало для планируемого project import: Input System `1.20.0`, AI Navigation `2.0.14`, glTFast stable `6.20.0`, minimum Unity `6000.0`. Позже в этой же сессии создан `unity/`: registry import и C# compile прошли, точные версии записаны в `Packages/manifest.json` и `packages-lock.json`. Это не равнозначно физической приёмке.

## Кандидаты для дальнейшего сравнения

| Область | Кандидаты | Что сравнивать документарно; не тестировалось |
|---|---|---|
| Character/controller | Character Controller (CC), ECM2, Opsive UCC | стоимость split-screen/4 seats, camera/animation integration, ownership состояния, возможность отключать правила; Opsive — полный FPS stack, CC/ECM2 — меньший motor-контур |
| Навигация | AI Navigation/NavMesh, A* Pathfinding Project | физически допустимые маршруты, явные переходы этажей, runtime updates и CPU/support cost; AI Navigation — первый кандидат, A* — альтернатива |
| Авторинг арен | фиксированная authored fixture, Dungeon Architect | arena fairness, spawn safety, sightline checks и экспорт проверяемых данных; DA документарно поддерживает модульную сборку и многоэтажные графы, но наши игровые проверки не предоставляет |

Для бесплатного полигона выбраны CharacterController и native NavMesh bake с параметрами capsule/step/slope из того же профиля motor. Платные альтернативы пока оценены документально, не установлены и не испытаны. Первичные ссылки roadmap: [Opsive UCC](https://opsive.com/assets/ultimate-character-controller/), [A* RecastGraph](https://arongranberg.com/astar/documentation/stable/recastgraph.html), [Dungeon Architect Unity](https://dungeonarchitect.dev/unity/).

## Зафиксированный первый срез и открытые решения

Первый proving slice: четыре реальных игрока; разрешены native physics и state-recorded replay (детали контракта — позже). Цель качества: FullHD–4K при `>=60 fps`. Открыты вопросы: окончательный hardware/quality matrix, internal scale, физические четыре gamepad и TV/вывод на экран. Покупки не предполагаются.

Идентичность browser baseline для сопоставления: commit `8844e40`, `v0.0.9`, profile `prototype-v1` revision `12`, schema `10`, hash `fnv1a64-v1:5edb6318d1ffaa45`; browser performance default seed `0x53544152`, medium/balanced, из исторического `scripts/run-browser-performance-gate.mjs`, scenario `match-slice-scenario-v2`. Это только source identity: legacy browser implementation удалена из текущего дерева 2026-09-21, а сопоставимый browser/Unity benchmark не проводился. Совместимость seed между browser и Unity не обещается. Отдельная диагностика native Player описана в handoff ниже. Main `07150e0` закрывает legacy backlog; старые элементы 6/9 не являются незавершёнными задачами.

Первичные источники версий: [Unity Input System 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.inputsystem.html), [AI Navigation 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.ai.navigation.html), [glTFast Editor import](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportEditor.md), [ECM2](https://oscar-gracian.gitbook.io/easy-character-movement-2). Дата проверки 2026-09-19; документация возможностей не доказывает совместимость платного пакета с полигоном.

## Результат первого полигона

Реализация, automated evidence, native screenshots и остающиеся physical/performance gates: [15-unity-four-player-proving-ground](tasks/15-unity-four-player-proving-ground.md). Это действующий handoff; инвентаризация пакетов в таблице выше относится к состоянию машины **до** создания проекта, фактически импортированные версии закреплены в `unity/Packages/`.
