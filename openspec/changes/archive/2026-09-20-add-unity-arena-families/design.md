## Context

См. proposal.md — Why и `specs/unity-arena-families/spec.md`. ARENA-1 уже владеет serializable definition, canonical fingerprint, structural validation и единственной Unity projection. Текущий generator monolithic и содержит только fixture layout; family choice отсутствует.

## Goals / Non-Goals

**Goals:**

- Сохранить одну generation/definition/projection boundary и добавить registry пяти pure family builders.
- Сделать family selection profile-owned, проверяемым и частью identity.
- Дать meaningful automated и muted native Player evidence на нескольких пространственно разных layouts.

**Non-Goals:**

- Не строить абстрактную generated-map навигацию или новые validator policies.
- Не вводить size, seed UX, Repeat lifecycle, fallback, replay, assets или renderer pipeline.
- Не закрывать physical, artistic или performance acceptance.

## Decisions

### Family — дискретное canonical profile value

Профиль `unity-arena-families-v1@1` получает integral descriptor `arena.family` с диапазоном 0–4 и стабильным ordinal mapping на пять публичных family IDs. Definition хранит resolved строковый ID; fingerprint сохраняет исходное profile value. Это использует действующий numeric descriptor registry и делает выбор явным, не добавляя второй тип profile.

Альтернатива — выбирать family по seed — отклонена: она смешивает две независимые оси configuration и противоречит утверждённому explicit selection. Строковый selector вне profile отклонён, потому что перестал бы быть частью canonical profile identity.

### Общие primitives, отдельные pure builders

`ArenaGenerator` валидирует request и dispatches по registry в один из пяти pure builders. Builders используют общие primitives для shell, supports, transitions и spawn catalog, но создают distinct semantic IDs/topology. `ProvingArena` не ветвится по family и продолжает строить только definition elements.

Альтернатива — пять Unity prefabs — отклонена: collision/presentation снова стали бы вторым spatial source.

### Structural checks остаются foundation-level

ARENA-2 расширяет только существующую structural completeness проверку: registered family ID, unique elements и минимум два declared transitions. Route reachability, fairness, capsule clearance и bounded-generation policy остаются ARENA-4/5.

## Risks / Trade-offs

- [Numeric family ordinal менее выразителен в Lab] → descriptor label/description и stable public ID в definition/tests делают mapping явным; полноценный selector UI не входит в срез.
- [Layouts могут формально строиться, но требовать последующей навигационной доработки] → PlayMode проверяет projection/NavMesh на representative endpoints; ARENA-3/4 gates остаются открытыми.
- [Пять builders увеличивают source file] → вынести registry/builders в отдельный arena source, сохранив definition types и validation boundary.

## Migration Plan

1. Добавить profile revision и family registry/builders с tests.
2. Перевести default native setup на явно выбранный shipped family, сохранив compatibility entrypoint.
3. Проверить EditMode/PlayMode, strict OpenSpec, Mac build и muted Player evidence.
4. Rollback — удалить один ARENA-2 commit; persisted replay/arena migration в scope отсутствует.
