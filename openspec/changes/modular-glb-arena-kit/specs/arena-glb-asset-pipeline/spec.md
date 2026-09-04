## Purpose

Capability задаёт поставляемый и воспроизводимый GLB-контракт модулей арены, чтобы renderer мог выразить утверждённый спортивный стиль, не становясь источником игровой геометрии.

## ADDED Requirements

### Requirement: Версионированный presentation asset manifest
Arena renderer SHALL получать каждый shipping architectural module только по стабильному manifest-ключу `clean-future-sport-glb-v1`. Manifest MUST задавать GLB URI, authored LOD0 и LOD1, semantic attachment kind, единицы и pivot contract, а отсутствующий либо невалидный entry MUST давать actionable presentation error до первого WebGL submission.

#### Scenario: Разрешение обязательного модуля
- **WHEN** renderer подготавливает validated ArenaDefinition с полным approved GLB kit
- **THEN** он разрешает portal facade, wall bay/buttress, route guide и landmark через manifest keys, а не через путь, вычисленный из surface ID

#### Scenario: Неполный manifest
- **WHEN** обязательный key, URI или LOD отсутствует либо GLB не проходит validation
- **THEN** runtime не начинает gameplay presentation и показывает стабильную причину без изменения simulation state

### Requirement: Пространственный и collision contract GLB-модуля
Каждый module SHALL быть glTF 2.0 binary (`.glb`) в метрах, с `+Y` вверх и локальной `+Z` front-осью, совместимой с Three.js. Pivot MUST находиться в semantic anchor: низ-центр для wall/buttress, floor-contact center для route guide, центр plane doorway для portal facade и центр combat region для landmark. Manifest MUST описывать authored collision proxy только как audit metadata; presentation runtime MUST NOT загружать или передавать proxy в collision, navigation, spawn, replay либо arena hash.

#### Scenario: Крепление к существующей spatial truth
- **WHEN** renderer размещает module на surface либо подтверждённом doorway gap
- **THEN** transform выводится из утверждённого anchor contract и существующего ArenaDefinition без добавления прохода, cover или collision solid

#### Scenario: Независимость collision identity
- **WHEN** authored GLB или его collision proxy заменяется совместимой visual revision
- **THEN** simulation, collision, navigation, spawn и replay identities остаются неизменными

### Requirement: Предсказуемый LOD и lifecycle
Каждый required module MUST поставлять LOD0 и LOD1 с одинаковыми pivot/attachment bounds и MUST позволять renderer выбрать LOD только по presentation distance. Runtime SHALL загрузить immutable source template, клонировать presentation instances, освобождать геометрии/materials после последнего owner и не выполнять WebGL submission на pause/hidden вследствие asset lifecycle.

#### Scenario: Смена LOD
- **WHEN** camera presentation distance пересекает manifest threshold
- **THEN** renderer переключает только visual instance между authored LOD без изменения gameplay transform, snapshot или hash

#### Scenario: Dispose
- **WHEN** browser session завершается или asset preparation fails
- **THEN** все созданные GLB presentation resources освобождаются, а последующих submissions или simulation ticks не появляется
