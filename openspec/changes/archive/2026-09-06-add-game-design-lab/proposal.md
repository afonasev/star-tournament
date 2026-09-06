## Why

Уже утверждённый `prototype-v1` содержит полный валидируемый профиль и descriptor registry, но дизайнер пока не может безопасно просматривать, изменять, сохранять и выбирать его ревизии. Лаборатория нужна, чтобы настраивать будущие матчи без неявной мутации текущей симуляции или поставляемого baseline.

## What Changes

- Добавить полноэкранную DOM Game Design Lab, доступную из главного меню.
- Показать все редактируемые поля `GameDesignProfile`, сгруппированные по domain-разделам, с metadata из единого descriptor registry.
- Добавить локальный draft, range/cross-field validation, создание новой immutable пользовательской ревизии и отдельный changelog `было → стало → дельта` относительно выбранной базы.
- Перенести контракт Spacewars: независимые нумерованные истории пользовательских профилей в `.local/`, versioned каталог опубликованных snapshots в `balance/releases.json` и одну переключаемую release reference.
- Добавить выбор сохранённой revision для следующего матча; по умолчанию новый матч использует exact release revision. Уже запущенный матч, его snapshot, replay, arena и state hash не меняются.
- Зафиксировать в канонической спецификации, что лаборатория не открывается из pause menu в этом change, а закрытие несохранённого draft требует явного Continue/Discard.

## Capabilities

### New Capabilities

- `game-design-lab`: полноэкранный DOM-процесс просмотра, редактирования, сохранения и выбора immutable ревизий игрового профиля для следующего матча.

### Modified Capabilities

- `game-design-profile-core`: локально созданные полные ревизии, их независимые profile histories и опубликованный release catalog становятся поддерживаемыми источниками профиля следующей match session.
- `match-loop-ui`: главное меню получает вход в лабораторию и отображает выбранную для нового матча profile identity.

## Impact

- Затрагиваются profile storage/revision utilities, Vite dev repository endpoint, checked-in release manifest, React DOM shell главного меню, start-match wiring и CSS лаборатории.
- Симуляция, renderer, input, camera, сетевой слой, asset pipeline и presentation profile не получают новой authority и не меняют существующий матч.
- Изменение следует `docs/GAME_SPEC.md` разделам 5–6 и архитектурным принципам раздела 7.
