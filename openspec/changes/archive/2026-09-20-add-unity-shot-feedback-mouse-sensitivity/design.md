## Context

См. proposal.md и delta specs. NativeCombatSession уже вычисляет направление, world occlusion, hit-zone и stopping distance каждой дробины, но публикует только shooter/damage; CombatPresentation запускает лишь fire pose. `SeatInputCoordinator` корректно разделяет mouse delta и gamepad rate, а `ProvingGround` содержит обычное Settings UI и образец сохранения FPS. Frozen match profile нельзя изменять настройкой во время матча.

## Goals / Non-Goals

**Goals:**

- Передать renderer immutable resolved shot data без повторного combat query.
- Показать bounded, пуллируемые/owned presentation effects, синхронизированные с session time.
- Дать Settings UI сохранённый и descriptor-validated mouse override только для paired keyboard/mouse seat.
- Проверить event, endpoints, preference, UI, lifecycle и Player evidence на одном и четырёх viewport.

**Non-Goals:**

- Не менять hitscan, spread, урон, hit zones, союзное blocking, physical projectiles или combat snapshot.
- Не добавлять assets, decals, replay, ARENA-3/4/5, online, integration, archive либо deploy.
- Не закрывать physical device/TV, artistic или reference-hardware performance acceptance.

## Decisions

### Shot notice — immutable результат authoritative resolution

NativeCombatSession сформирует один `ShotNotice` на допустимый accepted shot до применения damage. Он содержит sequence, shooter/life, session time, origin и value-only result каждой дробины: direction, endpoint, contact kind, normal и target/life identity. CombatPresentation подпишется на notice и никогда не вызывает raycast для визуального результата.

Альтернатива — повторять queries в renderer — отклонена: она расходится с analytic hit volumes, союзным blocking и поздней позой участника. Physical projectiles отклонены, поскольку меняют approved hitscan rules.

### Presentation lifecycle — owned bounded primitives

CombatPresentation создаёт collider-free short-lived tracer primitives от camera-appropriate visible muzzle к captured endpoint и contact-only impact primitives; их duration, speed, width, impact size и live cap принадлежат `unity-proving-ground-v1@1` descriptors. Эффекты используют session time, останавливаются на pause и полностью очищаются при expiry, Dispose, Menu, Repeat и unload. Local owner не видит дублирующий world tracer.

Альтернатива — persistent decals или physics particles — отклонена: они расширяют world state/lifecycle и усложняют cleanup без пользы для текущего feedback slice.

### Sensitivity — preference override, не mutable profile

Небольшой preferences helper читает `PlayerPrefs`, проверяет сохранённое число через descriptor `input.mouseDegreesPerPixel` и возвращает profile default для missing/invalid value. Settings UI строит label, unit, step и bounds по descriptor; SeatInputCoordinator использует resolved value только для paired mouse. Profile и frozen configuration не изменяются, gamepad/bot input не читает preference.

Альтернатива — multiplier — отклонена как вторая шкала. Мутация profile отклонена: она нарушает frozen Repeat identity.

## Risks / Trade-offs

- [Смерть скрывает presentation body до позднего event] → endpoints и source pose фиксируются в notice; effect не зависит от живого renderer object.
- [Дублирование owner/world tracer] → local view получает один camera-layer-safe effect от visible muzzle; чужие views получают world effect.
- [Runtime object leak при Repeat] → owned list/pool очищается тестами Dispose, repeat и additive unload.
- [Некорректный PlayerPrefs] → validate и fallback profile default; tests восстанавливают prior key.
- [Восьмиучастниковый burst засоряет бой] → profile-owned cap и короткие lifetimes; performance остаётся diagnostic gate.

## Migration Plan

1. Добавить immutable notice и PlayMode regression, сохранив `Fired` compatibility для HUD/poses.
2. Добавить profile descriptors, preference/UI/input с EditMode и PlayMode tests.
3. Добавить feedback primitives и lifecycle tests, затем strict OpenSpec, full Unity suites, Mac Player build и muted native evidence.
4. Rollback — удалить один change commit; persisted malformed keys безопасно игнорируются, migration gameplay state отсутствует.
