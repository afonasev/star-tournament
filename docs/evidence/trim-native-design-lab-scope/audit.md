# Аудит Balance Lab · 2026-09-29

## Выполнено по решению пользователя
Исключены из native Lab: authored геометрия/повороты/размеры solids; spawn positions/regions; ordered transition feet; route anchors; pickup anchors. Из оформления исключены layout coordinates, spawn-mark coordinates, wayfinding placement/yaw и размеры указателей, размеры деталей и решёток/рам, размещение и размеры светильников, экранов/вентиляторов, размеры/позиции космического backdrop. Это authoring уровня; значения и metadata сохраняются в runtime profiles и immutable histories. Новая UI-политика не меняет hash сама по себе.

## Остальные кандидаты (сейчас оставлены)

1. `navigation.sampleDistance` — fixture diagnostic projection radius. Поиск runtime потребителей дал только объявление descriptor; реальная навигация использует `bots.navigation.sampleDistance`. Самый явный кандидат на удаление из Lab.
2. `world.maximumTransitionSegment` — граница длины authored transition segment; используется `ArenaDefinitionValidator`, то есть валидатором карт. Относится к техническим authoring проверкам.
3. `world.minimumSupportHeight` — нижняя граница world support query для тела; используется `CombatPresentation`, поэтому удаление из runtime недопустимо. UI можно скрыть как технический world bound.
4. `simulation.fixedTickHz` — частота native gameplay/motor ticks, влияет на точность и нагрузку. Это инженерный режим исполнения; изменение может повлиять на матч и физику. Предлагается убрать из обычной Lab при отдельном решении.
5. `light.shadowResolution`, `light.shadowBias`, `light.shadowNormalBias`, `light.shadowCasters` — технический rendering/performance набор. Разрешение/смещения теней логичнее в техническом конфиге. Контраст, интенсивность, дальность света и цвет оставлены как читаемость сцены.
6. `bots.navigation.heightTolerance`, `bots.navigation.sampleDistance` — допуски native navigation provider; осторожно отделить от игровых параметров AI (реакции, точности, stuck/recovery/repath). Они действуют в runtime; одного названия недостаточно для удаления.

## Полезные параметры, которые оставлены
Урон/боезапас/cooldown, здоровье/броня/бонусы, движение/capsule, чувствительность/камера/killcam, правила матча/очки, реакция/прицел/поведение ботов. Presentation параметры оружия/хвата/анимации/длительности эффектов оставлены: это настройка читаемости действия. `view.switchClearance` параллельной change animate-weapon-switch также остаётся: относится к анимации оружия, а не уровню. Светлота материалов, палитра зон, прозрачность стекла, яркость света/экранов и скорость декоративного движения остаются.

## Пределы
Аудит — статический поиск actual runtime consumers и metadata. Остальные кандидаты не скрыты автоматически: исходный запрос однозначно исключает только геометрию и координаты уровней. Player review является автоматизированной QA, human acceptance отдельно.
