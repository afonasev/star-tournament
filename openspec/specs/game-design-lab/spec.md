# game-design-lab Specification

## Purpose

Capability предоставляет дизайнеру изолированную полноэкранную поверхность для создания, проверки и выбора неизменяемых профилей будущих матчей без влияния на уже работающую симуляцию.

## Requirements

### Requirement: Полноэкранная лаборатория и покрытие профиля
Главное меню SHALL открывать отдельную полноэкранную DOM Game Design Lab. Она MUST показывать все и только редактируемые numeric fields выбранного `GameDesignProfile`, сгруппированные по игроку/движению, дробовику, матчу/scoring, смерти/respawn, ботам и generator presets `small`/`medium`/`large`. Каждое поле MUST выводить label, description, unit, hard minimum/maximum и step из единого descriptor registry; технические invariants и presentation profile MUST NOT отображаться в этой лаборатории.

#### Scenario: Открытие repository release
- **WHEN** пользователь открывает Game Design Lab без local selection
- **THEN** UI показывает repository release profile, его revision и content hash, а все числовые поля имеют descriptor-derived metadata

#### Scenario: Полнота видимых полей
- **WHEN** descriptor registry profile проверен автоматическим или UI audit
- **THEN** каждое редактируемое numeric field появляется ровно в одной группе, а технические invariants и renderer-only presentation fields отсутствуют

### Requirement: Draft, validation, profiles и immutable ревизия
Лаборатория SHALL создавать local полный draft от выбранной сохранённой revision и MUST валидировать range, step и cross-field relationships до сохранения. Она MUST предоставлять отдельный выбор profile и revision, а создание нового profile MUST начинать его независимую историю от выбранной base revision. Сохранение валидного draft MUST создавать новую immutable пользовательскую revision с новой identity и content hash; shipped `prototype-v1` MUST оставаться неизменяемым. Невалидный draft MUST оставаться видимым и объяснять field paths/errors, не становясь доступным для запуска матча.

#### Scenario: Сохранение изменения
- **WHEN** пользователь изменяет допустимое поле draft и сохраняет его
- **THEN** создаётся новая полная local revision в истории выбранного profile, исходная revision не меняется, а новая revision становится выбираемой для следующего матча

#### Scenario: Ошибка draft
- **WHEN** draft имеет значение вне descriptor range/step либо нарушает cross-field validation
- **THEN** сохранение заблокировано, UI показывает локальную понятную ошибку, а прежние сохранённые revision остаются без изменений

### Requirement: Changelog и намеренное закрытие draft
Лаборатория SHALL показывать отдельный Changelog относительно выбранной base revision в стабильном порядке group → object → field с old value, new value и signed delta. Если несохранённый draft имеет изменения, попытка закрыть лабораторию MUST запросить явное действие `Продолжить редактирование` либо `Отбросить`; закрытие без решения MUST NOT тихо потерять draft.

#### Scenario: Просмотр изменений
- **WHEN** пользователь меняет несколько profile fields
- **THEN** Changelog показывает каждое отличие от base revision один раз с прежним, новым и signed delta значениями

#### Scenario: Закрытие изменённого draft
- **WHEN** пользователь закрывает лабораторию с несохранёнными изменениями
- **THEN** UI остаётся в лаборатории до выбора Continue или Discard; Discard возвращает к главному меню без создания revision

### Requirement: Выбор revision только для следующего матча
Лаборатория SHALL позволять выбрать сохранённую valid revision как профиль следующего нового матча. Выбранная revision MUST быть видна в предматчевом меню по identity/content hash. Лаборатория MUST NOT быть доступна из pause menu в этом change; уже созданные match session, arena, snapshot, replay, state hash и renderer MUST NOT меняться от draft, сохранения либо выбора revision.

#### Scenario: Новый матч с выбранной revision
- **WHEN** пользователь сохраняет и выбирает локальную revision, возвращается в меню и запускает новый матч
- **THEN** match session валидирует и использует exact выбранные profile identity/hash для configuration, generation и simulation

#### Scenario: Текущий матч не меняется
- **WHEN** существует запущенный или paused матч, а пользователь ранее сохранил другую revision
- **THEN** этот матч продолжает использовать исходные profile identity/hash и не предлагает открыть лабораторию из pause menu

### Requirement: Явная promotion релизной revision
Лаборатория SHALL явно помечать release profile и точную release revision в отдельных selectors, а также в identity выбранного snapshot. Только на dev-стенде valid saved revision MAY быть помечена действием «Сделать релизной версией». При успешной promotion UI MUST сразу показать новую release identity; исторические snapshots MUST остаться неизменными.

#### Scenario: Выбранная revision уже релизная
- **WHEN** selected revision совпадает с repository `releaseRef`
- **THEN** действие promotion disabled и UI явно помечает revision как релизную

#### Scenario: Очистка нерелизной ветки
- **WHEN** selected local profile не содержит current release revision
- **THEN** UI предлагает удалить только профиль целиком; отдельные revisions не имеют delete controls
