## Context

См. `proposal.md`. `prototype-v1` уже является полным immutable profile с descriptor registry, validation и content hash; browser shell всегда запускает baseline напрямую. Лаборатория добавляет локальный lifecycle ревизий и menu routing, не передавая UI authority над simulation.

## Goals / Non-Goals

**Goals:**

- Хранить полные валидные пользовательские snapshots в versioned `.local/` history, а опубликованные snapshots и current release reference — в checked-in manifest.
- Собрать DOM editor из descriptor registry: группы, numeric inputs, inline validation и стабильный Changelog.
- Передавать выбранный exact snapshot только в создание нового runtime/match session.

**Non-Goals:**

- Не редактировать shipped profile, не менять текущую/paused session и не добавлять live-tuning.
- Не редактировать `presentation-balanced-v1`, audio, input bindings, network, split-screen или renderer settings.
- Не создавать новые gameplay parameters либо правила; change только раскрывает существующий profile core.

## Decisions

### Локальная история отделена от checked-in release catalog

По модели Spacewars файл `balance/releases.json` хранит schema-versioned manifest опубликованных полных snapshots и единственный `releaseRef`. Рабочая история пользовательских профилей хранится в `.local/balance-profiles.json`: это map `profile id → immutable revisions`, где revision number инкрементируется только внутри своего profile id. Перед записью snapshot проходит существующую validation и canonical hashing; отдельную revision изменять или удалять нельзя. Shipped `prototype-v1` остаётся read-only base и всегда доступен как recovery choice.

Публикация на dev-стенде записывает exact выбранный snapshot в `balance/releases.json` (если его там ещё нет) и атомарно переключает `releaseRef`; предыдущие published snapshots остаются в каталоге. Endpoint использует optimistic version check, same-origin guard и atomic write. Build валидирует manifest. Production build только читает manifest, поэтому действие публикации там недоступно.

Это сохраняет replay/startup identity и исключает partial overrides. Альтернатива — сохранять только diff — отвергнута: получившаяся effective configuration зависит от mutable baseline и хуже переносится между revision/schema.

### Draft живёт только в UI до явного Save

Lab создаёт deep-copy выбранной base revision в React-local draft. Поле применяет descriptor step/range для ввода, а полный validation запускается для save; cross-field errors возвращаются к stable paths. Changelog вычисляется как `draft − base` по registry order и не является источником данных.

Это даёт понятный путь исправления ошибок и гарантирует, что match никогда не получает несохранённый draft. Альтернатива — live-save каждого input — отвергнута, потому что она создаёт шумные ревизии и делает отмену небезопасной.

### New-match boundary — единственная точка применения

Главное меню получает selected revision от repository и передаёт exact validated profile при построении следующей configuration/arena/runtime. Если пользователь не выбрал отдельный saved revision, menu использует repository `releaseRef`; уже созданный runtime замыкает исходный profile; pause не получает Lab action. Generator draft влияет только на следующую arena generation по действующему контракту profile core.

Это отделяет DOM от simulation и не требует runtime profile swap. Альтернатива — применить profile после Resume — отвергнута решением пользователя: изменения предназначены для следующего матча.

### Полноэкранный DOM route без gameplay input

Lab заменяет pre-match DOM surface, не создаёт WebGL/canvas и не захватывает pointer lock. Закрытие чистого draft возвращает в menu; для dirty draft появляется confirmation. В single-seat текущего slice нет gamepad slots/split-screen в Lab, поэтому gamepad reconnect, viewport join/leave и per-seat camera policy не меняются. Keyboard/mouse focus остаётся обычным DOM focus, а после возврата старт матча проходит существующий явный pointer-lock flow.

## Risks / Trade-offs

- [Local history или release manifest повреждён/устарел] → versioned parser валидирует каждую revision и не подставляет другую автоматически; menu показывает actionable error.
- [Два дизайнера публикуют одновременно] → repository endpoint сверяет version файла, возвращает conflict и не перезаписывает чужой release.
- [Большой набор generator fields перегружает экран] → groups и native summary/section navigation, а Changelog вынесен в отдельную view.
- [Отключение старой revision при изменении schema] → сохранить migration boundary в storage parser; неподдерживаемый snapshot нельзя запускать и он не меняет shipped baseline.
- [UI ошибочно дублирует metadata] → controls и Changelog адресуют registry paths, а tests аудируют coverage/uniqueness.

## Migration Plan

1. Добавить canonical decision в `docs/GAME_SPEC.md`, checked-in manifest и revision repository model с baseline fallback только при отсутствии selected local ref.
2. Добавить unit/component/dev-server tests, затем browser playtest с muted audio: открытие, field metadata, invalid draft, profile/revision selection, publication, save/select, new-match identity и dirty close.
3. При rollback удалить editor route/repository adapter и manifest; existing shipped `prototype-v1` startup сохраняется.
