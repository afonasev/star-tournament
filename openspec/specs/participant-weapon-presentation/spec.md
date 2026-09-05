# participant-weapon-presentation Specification

## Purpose

Capability задаёт читаемое renderer-only представление спортивных роботов и их оружия, чтобы участник и его экипировка были различимы в бою без влияния на детерминированные правила матча.

## Requirements

### Requirement: Лёгкий робот с цветовой идентичностью участника
Renderer SHALL показывать каждого живого non-local участника как локальный manifest-addressed GLB лёгкого человекоподобного спортивного робота с LOD0/LOD1, fixed meter/axis/pivot contract, читаемым силуэтом, chest core, visor, плечевыми панелями и rear beacon. Authoritative participant color MUST быть одновременно виден как минимум на core, visor и плечевых панелях; renderer MUST NOT подменять его архитектурным accent-цветом или hardcoded общим цветом. Модель и texture maps MUST использовать уже поставленный presentation-owned GLB/template cache, texture lifecycle и graphics-quality tiers, не создавая второго loader или residency policy.

#### Scenario: Два живых участника с разными цветами
- **WHEN** snapshot содержит двух живых non-local участников с разными participant colors
- **THEN** renderer показывает два отличимых робота, и цвет каждого виден на его core, visor и плечевых панелях

#### Scenario: Уничтожение и возрождение участника
- **WHEN** health участника достигает нуля, а затем его следующая жизнь становится alive
- **THEN** renderer показывает визуальное уничтожение без gameplay collision и восстанавливает ту же authoritative color identity на живом роботе

### Requirement: Third-person оружие участника
Renderer SHALL показывать у каждого живого non-local участника его поддерживаемое локальное manifest-addressed GLB оружие, визуально закреплённое в обеих руках и направленное по participant yaw. Для `double-barrel-shotgun` модель MUST иметь два отчётливо разнесённых ствола, компактную energy chamber и owner-color indicator; LOD0/LOD1 и textures MUST следовать существующему `clean-future-sport-glb-v1` asset contract.

#### Scenario: Видимое оружие stationary fixture
- **WHEN** живой stationary fixture с `double-barrel-shotgun` виден в игровом viewport
- **THEN** рядом с руками робота виден двухствольный энергодробовик, ориентированный вместе с участником

#### Scenario: Неподдерживаемая presentation identity
- **WHEN** renderer получает participant либо weapon identity без поддерживаемой presentation model
- **THEN** он не меняет simulation state и показывает стабильный нейтральный fallback без runtime error

### Requirement: First-person видимое оружие и руки
В active first-person viewport renderer SHALL показывать camera-attached GLB viewmodel с двумя роботизированными предплечьями и поддерживаемым оружием в нижней правой части кадра, не закрывая центральный прицел. Viewmodel MUST использовать тот же weapon semantic identity, что и local participant, но MUST NOT входить в snapshot, replay либо simulation hash.

#### Scenario: Начальный first-person кадр
- **WHEN** local participant начинает матч с поддерживаемым `double-barrel-shotgun`
- **THEN** первый WebGL кадр показывает две роботизированные руки и читаемый двухствольный дробовик в нижней правой части viewport

#### Scenario: Resize без нового tick
- **WHEN** paused first-person viewport меняет размер и presentation invalidated
- **THEN** renderer делает один redraw с корректно расположенной viewmodel и не меняет simulation state

### Requirement: Presentation-only реакция выстрела
После versioned `shotgun-shot` event renderer SHALL показывать двойную muzzle flash на концах обоих стволов и краткую умеренную отдачу first-person viewmodel. Продолжительность и каденс эффекта MUST быть derived из существующего presentation tick/event contract и MUST NOT менять ammo, hit result, cooldown или damage.

#### Scenario: Допустимый выстрел
- **WHEN** simulation публикует `shotgun-shot` event для local participant
- **THEN** оба ствола first-person оружия дают краткую вспышку и viewmodel кратко смещается отдачей, а HUD ammo берёт значение из simulation snapshot

#### Scenario: Dry fire
- **WHEN** fire press не создаёт `shotgun-shot` event из-за пустого боезапаса
- **THEN** renderer не показывает muzzle flash или отдачу как успешный выстрел

### Requirement: Authored equipment surface detail

Робот, world shotgun и first-person shotgun SHALL показывать фактуру в масштабе оборудования: светлое покрытие, металлические узлы и тёмные рукояти, фаски и конструктивно сгруппированные детали. Основные оболочки SHALL использовать сужающиеся многогранные или округлые формы вместо крупных прямоугольных плоскостей. Identity-зоны MUST оставаться читаемыми; визуальная детализация MUST NOT менять simulation state.

#### Scenario: Close equipment inspection
- **WHEN** игрок видит робота или дробовик вблизи
- **THEN** различимы фаски, крепёж, панели и фактура поверхностей без наложенной текстуры архитектурной стены

#### Scenario: Reduced detail
- **WHEN** renderer использует LOD1
- **THEN** сохраняются основные панели, два ствола и цветовая идентификация с сокращёнными вторичными деталями

#### Scenario: Resource lifecycle
- **WHEN** матч закрывается после загрузки моделей
- **THEN** renderer освобождает принадлежащие GLB текстуры и допускает повторный запуск

### Requirement: Slender robot and connected first-person grip

Робот SHALL иметь узкую талию, компактные плечи и тонкие броневые секции конечностей. First-person модель SHALL показывать две различимые кисти, обхватывающие рукоять и цевьё, с непрерывными предплечьями за нижней границей кадра. Геометрия MUST сохранять контакт при отдаче и не менять hit volumes.

#### Scenario: First-person idle and recoil
- **WHEN** игрок видит оружие в покое или при отдаче
- **THEN** обе кисти касаются своих точек хвата, их запястья соединены с предплечьями и обрубленные концы не висят внутри viewport

#### Scenario: Slender silhouette at reduced detail
- **WHEN** renderer выбирает LOD1
- **THEN** остаются стройные пропорции робота и обе связные цепи локоть-запястье-кисть

#### Scenario: Persistent first-person closeup
- **WHEN** руки постоянно видны с камеры игрока
- **THEN** предплечья SHALL показывать стыки панелей, уплотнения, утопленный крепёж и направленную металлическую фактуру; LOD1 сохраняет основные швы

#### Scenario: Robot detailing with accepted weapon preserved
- **WHEN** отображается детализированный робот
- **THEN** SHALL быть видны швы нагрудной/ножной брони, крепёж и металлические обоймы; основные швы сохраняются в LOD1, принятые first-person и weapon GLB остаются неизменными
