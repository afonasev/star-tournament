## Context

См. `proposal.md`. Bootstrap уже передаёт выбранный validated profile при создании initial snapshot. Однако parser сверяет вложенную identity с module-level runtime singleton (и legacy v6), а не с profile активной сессии.

## Goals / Non-Goals

**Goals:**

- Сделать exact selected profile явным compatibility context parse/serialize/hash и playable step.
- Сохранить deterministic early rejection identity, не совпадающей с активной сессией.

**Non-Goals:**

- Не менять profile schema, release catalog, local revision storage, arena/replay payload schema или gameplay balance.

## Decisions

- Parser получает validated profile context рядом с configuration и проверяет snapshot identity against него. Это сохраняет trust boundary: произвольная identity не становится допустимой только потому, что существует в registry.
- Default остаётся current shipped profile для существующих callers. Browser bootstrap и step передают active profile explicitly; legacy v6 remains readable only where already supported.
- Serializer/hash используют тот же optional context, чтобы их validation не расходилась с parser.

## Risks / Trade-offs

- [Непрокинутый context в replay/headless caller] → тестами покрыть default и explicit profile paths; runtime/step передают active profile.
- [Случайное принятие несовместимого snapshot] → test фиксирует rejection при differing revision/hash.

## Migration Plan

Изменение не мигрирует persisted data. Rollback — откатить единственный change commit; existing current-profile behavior остаётся baseline.
