## Why

Текущий renderer-native kit уже передаёт спортивный ритм, но состоит из процедурных примитивов и не задаёт воспроизводимый shipping-контракт для художественных модулей. Следующий проход нужен, чтобы приблизить арену к утверждённому эскизу полноценными оптимизируемыми GLB-модулями, сохраняя неизменной единственную spatial truth симуляции.

## What Changes

- Ввести renderer-owned GLB asset kit `clean-future-sport-glb-v1` для portal facade, wall bay/buttress, floor route guide и central landmark, загружаемый только через стабильные manifest-ключи.
- Зафиксировать контракт поставки: glTF 2.0 binary (`.glb`), метры как единицы сцены, локальные оси Three.js, pivot в семантической точке крепления и обязательный authored LOD0/LOD1 для каждого модуля.
- Заменить существующие крупные renderer-native primitive forms экземплярами GLB kit, привязанными исключительно к имеющимся semantic surfaces и подтверждённым doorway gaps.
- Описать collision proxy в asset manifest как presentation-only audit metadata: runtime НЕ загружает и НЕ использует его для collision, navigation, spawn или arena hash.
- Сохранить material tokens, bright studio-light, participant contrast и lifecycle/performance contract; GLB-материалы не получают team/participant colors.
- Обновить `docs/GAME_SPEC.md`, tests и browser/performance evidence для нового asset pipeline.

Не входят в scope: новая topology, вертикальные маршруты, изменение `ArenaDefinition`, collision/navigation/spawn, механики, input/split-screen, animation, texture streaming, внешняя CDN/asset service и сетевой режим.

Открытые продуктовые решения отсутствуют: пользователь явно поручил закрыть asset-pipeline развилки в этом change. Полный художественный ассортимент сверх первого modular kit остаётся отдельной будущей change.

## Capabilities

### New Capabilities

- `arena-glb-asset-pipeline`: версия, manifest, spatial contract, LOD и lifecycle поставляемых GLB-модулей для presentation-only procedural arena renderer.

### Modified Capabilities

- `arena-presentation-style`: крупные архитектурные формы `clean-future-sport-v1` переходят от renderer-native primitives к manifest-addressed GLB kit без изменения spatial identity.
- `browser-runtime-foundation`: browser runtime асинхронно подготавливает и корректно освобождает presentation assets, сохраняя pause/hidden и performance contract.

## Impact

Затрагиваются `docs/GAME_SPEC.md` §§2, 7–8, `openspec/specs/arena-presentation-style`, `openspec/specs/browser-runtime-foundation`, renderer, его unit tests, production build, performance gate и muted in-app Browser evidence. Новая runtime dependency не требуется: используется уже установленный Three.js GLTFLoader; simulation, Rapier и serializable state не меняются.
