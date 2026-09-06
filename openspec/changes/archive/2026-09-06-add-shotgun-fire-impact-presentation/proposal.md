## Why

Текущая краткая двойная вспышка и единый impact не передают характер дробового выстрела и воспринимаются как заглушка. Кинетическая подача с читаемыми отдельными дробинами даст мгновенную, правдоподобную обратную связь о направлении и результате выстрела, сохранив спортивный sci-fi тон арены.

## What Changes

- Заменить энергетический характер fire feedback двухстволки на кинетический: видимый дульный всплеск, дым/частицы и короткие трассы отдельных дробин из обоих стволов.
- Показывать material-aware impact feedback для каждой завершившейся дробины: искры и сколы на архитектуре, искры/обломки покрытия на роботах, без крови, gore или изменения hit result.
- Собирать эффекты только из существующих versioned combat events и presentation данных; simulation, баланс дробин, collision, damage, input и HUD-правила не меняются.
- Проверить эффект в muted first-person browser playtest и зафиксировать актуальные screenshots из изолированного dev-стенда.

## Capabilities

### New Capabilities

- `shotgun-kinetic-presentation`: Производственная renderer-only подача выстрела и завершений дробин двухстволки.

### Modified Capabilities

- `double-barrel-shotgun-combat`: Уточняет, что combat feedback должен представлять отдельные дробины и их попадания без изменения детерминированного боя.
- `participant-weapon-presentation`: Заменяет энергетический характер muzzle feedback на кинетический presentation-only выстрел.

## Impact

Затрагиваются renderer combat-effects/viewmodel, адаптация existing shotgun/damage event данных и browser visual QA. Зависимости, GLB manifest, audio, deterministic simulation, GameDesignProfile и public API не меняются. Решение относится к разделам 2 и 3 `docs/GAME_SPEC.md`: спортивный sci-fi visual direction, renderer/simulation boundary и первый двухствольный дробовик.
