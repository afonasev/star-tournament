# Native bot tactics

`improve-native-bot-tactics` развивает общий native planner FFA/teams. Физика, damage, ресурсы, pickup lifecycle и weapon-switch fire boundary остаются общими с людьми.

## Владельцы данных

- `NativeBotObservationProvider`: единственный фильтр enemy FOV/LOS; точные HP/броня поступают только вместе с прямым наблюдением.
- `NativeBotPerception`: dated память, движение из последовательных наблюдений одной жизни, direct-only team reports с исходным временем. Snapshot v2; никаких обновлений скрытого состояния.
- `NativeBotPickupKnowledge`: redacted каталог появлений/исчезновений бонусов и weapon pickups; положительная задержка, bootstrap/respawn через тот же канал. Planner получает только доставленные события, без collector и respawn timer. Snapshot v1 сохраняет очереди и revision ordering.
- `NativeBotPlanner`: потребность, цена фактического маршрута, риск из памяти, наблюдаемая живучесть, устойчивость намерения и ограниченный retry. Weapon acquisition/refill — самостоятельные цели, collection делает исключительно `NativeCombatSession`.
- `NativeShotgunPolicy` и `INativeRocketSurfaceTactics`: utility доступных оружий, burst/continuous окно, cooldown/switch costs; Pulse surface/body aim с наблюдаемым lead, первый WorldLayer/body контакт и self/allied splash с прогнозом собственного/союзного движения.
- `NativeBotMatchDriver`: замороженные allowlisted кадры перед session tick, снимок всех AI owners; restore сначала валидирует всех владельцев. Native routes при успешном restore перепроверяются по текущей геометрии.

## Balance Lab

`unity-bot-behavior-v1@2`, `unity-bot-perception-v1@2` и `native-bot-evaluation-v1@1` содержат все новые numeric descriptors. Evaluation registry read-only: изменение порогов в UI не может подменить preregistered gate. История Lab сохраняет старые immutable snapshots и добавляет совместимые revision, не перезаписывая hash.

## Проверка

Протокол фиксируется до tuning в shared planning change `evaluation-protocol.json`. Tuning seed отделены от контрольных; control — попарные FFA/teams с перестановкой roster slots/сторон. `NativeBotTacticsReview` выполняет реальные session/driver ticks и сохраняет per-run JSON, raw standings, damage, survival, pickups, idle/blocked, решения, weapon shots и safety rejections. `tools/evaluate-native-bots.py` проверяет полноту серии, практически значимые пороги и bootstrap uncertainty по независимым seed с сохранением пар перестановок.

`-botTacticsReview -botTacticsEvaluation -botTacticsEvidence /absolute/path` — tuning серия; `-botTacticsControl` выбирает отдельные control seed. Без evaluation-флага выполняется muted обычный Player QA mixed FFA/teams, AI viewport, natural death/respawn, snapshot restore, pause и Repeat с PNG/JSON.

Native diagnostics на текущем Mac не являются приёмкой reference hardware FPS или physical controllers. Exact Player human acceptance хранится отдельно в shared delivery record.
