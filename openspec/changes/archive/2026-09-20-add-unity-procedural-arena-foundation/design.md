## Context

См. proposal.md и `unity-procedural-arena-foundation` spec. Сейчас `ProvingGround` создаёт `ProvingArena`, а та вычисляет fixture geometry из runtime profile. Это сохраняет текущий маршрут, но не предоставляет отдельной arena identity и смешивает generation с Unity-проекцией.

## Goals / Non-Goals

**Goals:**

- Выделить pure serializable definition и deterministic generation input до native session.
- Оставить `ProvingArena` Unity-adapter, который строит current physical/presentation fixture только из definition.
- Зафиксировать profile metadata для всех новых spatial values и небольшие, независимые seams для families/validators.

**Non-Goals:**

- Новая topology, spatial family, size preset, fairness/route validation, fallback или replay schema.
- Изменение input, cameras, HUD, bot decision policy, assets, network или browser baseline.

## Decisions

### Data-only definition перед Unity adapter

`ArenaGenerationRequest`, `ArenaDefinition` и structural primitives будут сериализуемыми data-only types; generator и validators не создают GameObject. `ProvingArena.Build` получает accepted definition и остаётся единственным Unity projection. `SafeSpawnSelector` читает definition-owned regions/bounds/slots, а `NativeNavigationProvider.Identity` составляется из accepted arena identity и movement geometry fingerprint. Это устраняет parallel spatial sources без обещания strict cross-platform simulation determinism.

Альтернатива — хранить seed на `ProvingArena` и продолжить вычислять geometry во время Build — отклонена: она не даёт independently testable definition или future validator boundary.

### Один foundation family и profile-owned geometry

Регистрируется один `unity-two-level-fixture-v1` family. Его geometry читает только новый `unity-arena-foundation-v1@1` profile с descriptor metadata; canonical ordered values fingerprint включается в definition identity, поэтому mutable profile с теми же id/version не может выдать ложную identity. Current `ProvingProfile` остаётся владельцем motor/capsule настроек и не становится arena tuning registry.

Альтернатива — добавить fixture размеры к broad `ProvingProfile` — отклонена: identity и balance ownership были бы неоднозначны.

### Bounded validation до native session

Generator создаёт definition, запускает identity/structural validators и возвращает accepted definition либо stable exception. `ProvingGround` разрешает default request с fixed seed для обычного setup; compatibility overload `Build(ProvingProfile)` генерирует тот же default definition только для существующих tests/adapters.

## Risks / Trade-offs

- [Текущие geometry tests ожидают прежние coordinates] → foundation profile получает shipped values, а PlayMode подтверждает NavMesh/routes/spawns.
- [Future topology потребует richer primitives] → definition хранит typed elements/semantic IDs и registry interfaces, не воображая будущие rules заранее.
- [Identity hash может непреднамеренно зависеть от runtime formatting] → canonical ordinal serialization и EditMode same-input/different-input tests.

## Migration Plan

1. Ввести pure arena generation data/profile/validator classes и tests.
2. Перевести `ProvingGround` и `ProvingArena` на accepted definition с compatibility Build overload.
3. Прогнать EditMode, PlayMode, native Mac build и muted Player diagnostic; сохранить JSON/PNG evidence.
4. Rollback — удалить один change commit: existing scene has no persisted arena/replay data and no external migration.
