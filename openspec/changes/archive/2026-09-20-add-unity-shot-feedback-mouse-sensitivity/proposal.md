## Why

В native Unity Player визуально не читается путь отдельных дробин и результат попадания, а поворот мышью ощущается медленным и не может быть настроен игроком. Нужен один ограниченный срез, который делает существующий hitscan-дробовик наблюдаемым, а look — управляемым без изменения authoritative боя или lifecycle захвата курсора.

## What Changes

- Добавить derived presentation для существующего `shotgun-shot`: короткий видимый полёт отдельных дробин и material-aware impact для попадания или дальнего окончания луча, с bounded lifetime и без persistent decal.
- Добавить в обычное Unity Settings UI сохранённое пользовательское override-значение чувствительности мыши в тех же градусах на пиксель; UI читает descriptor metadata, валидирует диапазон и показывает понятную единицу.
- Сохранить разделение: NativeCombatSession остаётся единственным владельцем spread, hit, damage и range; настройка влияет только на нормализацию mouse delta в action frame, использует profile default при отсутствии preference и не меняет gamepad input.
- Сохранить mouse focus/pointer-lock contract: настройка доступна только вне captured gameplay и не снимает/не захватывает курсор сама.
- Добавить targeted EditMode/PlayMode coverage, muted native Player evidence (default и крайние sensitivity, дробь в полёте и impact), Mac build и handoff.

## Capabilities

### New Capabilities

- `unity-shot-feedback-and-mouse-sensitivity`: native Unity contract для наблюдаемой derived-дроби и persistable пользовательской mouse sensitivity.

### Modified Capabilities

- `first-person-player-control`: чувствительность mouse look становится пользовательским persistable override profile-defined sensitivity при сохранении focus/capture границы.
- `participant-weapon-presentation`: shotgun-shot feedback получает читаемые flight и impact состояния, derived из существующего combat event.

## Impact

Затрагиваются `docs/GAME_SPEC.md`, Unity profile/descriptor registry, `SeatInputCoordinator`, normal Settings UI/lifecycle в `ProvingGround`, `CombatPresentation`, native EditMode/PlayMode tests, Mac Player build и evidence/handoff. Не затрагиваются rules дробовика, replay, ARENA-3/4/5, сетевой режим, assets, integration, archive или deploy.
