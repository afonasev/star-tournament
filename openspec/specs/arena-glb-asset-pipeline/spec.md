# arena-glb-asset-pipeline Specification

## Purpose

Capability задаёт поставляемый и воспроизводимый GLB-контракт модулей арены, чтобы renderer мог выразить утверждённый спортивный стиль, не становясь источником игровой геометрии.

## Requirements

### Requirement: Версионированный presentation asset manifest
Arena renderer SHALL получать каждый shipping architectural и participant module только по стабильному manifest-ключу `clean-future-sport-glb-v1`. Manifest MUST задавать GLB URI, authored LOD0 и LOD1, semantic attachment kind, единицы и pivot contract, а отсутствующий либо невалидный entry MUST давать actionable presentation error до первого WebGL submission. Local participant keys `light-sport-robot`, `double-barrel-shotgun` и `first-person-shotgun` MUST продолжать разрешаться через тот же immutable template cache, а не через отдельный participant loader.

#### Scenario: Разрешение обязательного модуля
- **WHEN** renderer подготавливает validated ArenaDefinition с полным approved GLB kit
- **THEN** он разрешает portal facade, wall bay/buttress, route guide, landmark, light sport robot, world double-barrel shotgun и first-person shotgun через manifest keys, а не через путь, вычисленный из surface ID или participant ID

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

### Requirement: Совместимый detail replacement participant assets
Detail revision participant GLB MUST сохранять для каждой LOD-пары manifest key, fixed meter/+Y-up/+Z-front/pivot contract, attachment bounds и semantic presentation role. LOD0 MUST содержать materially denser authored form than LOD1, with separately auditable panel, joint, hand/finger, bore, coil, vent, rail and identity semantics where applicable; both LODs MUST retain every required readable role feature. Detail replacement MUST NOT introduce simulation collision geometry, textures outside the existing texture lifecycle, gameplay metadata or a new runtime asset policy.

#### Scenario: Выбор LOD participant asset
- **WHEN** graphics tier либо presentation distance выбирает LOD0 или LOD1 робота либо оружия
- **THEN** выбранная модель сохраняет тот же attachment anchor, readable role features и renderer-only lifecycle без изменения participant transform, snapshot или hash

#### Scenario: Asset audit detail pass
- **WHEN** shipping participant GLB pairs проходят source audit до browser build
- **THEN** audit подтверждает для каждой пары валидный GLB, одинаковый spatial contract LOD0/LOD1, LOD0 feature-density uplift и наличие role-specific panel/joint/hand/bore/coil/vent/rail semantics без обращения к simulation state
