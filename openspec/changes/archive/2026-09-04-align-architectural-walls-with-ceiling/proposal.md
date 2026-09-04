## Why

Центральные primary walls останавливаются ниже потолка из-за внутреннего height cap, хотя внешняя shell и ceiling выше. Это создаёт визуально случайные разрывы и не соответствует утверждённому архитектурному правилу.

## What Changes

- Primary corridor и room walls получают полную profile-defined высоту до потолка.
- Пониженными остаются только явно семантические buttress/cover препятствия.
- Обновляются deterministic arena identities и их fixture expectations; collision, navigation и spawn продолжают валидироваться до матча.

## Capabilities

### New Capabilities

- `architectural-height-semantics`: Разделение full-height primary walls и intentional low cover obstacles в canonical arena geometry.

### Modified Capabilities

- `procedural-arena-generation`: Генератор требует выравнивать primary walls с profile-defined ceiling height.

## Impact

Затрагиваются procedural generator, deterministic arena/profile fixture hashes, validation tests и `GAME_SPEC` §§2, 6–8. Renderer получает уже корректную canonical geometry; новых зависимостей нет.
