# «Колодец»: исследование света, 18.10.2026

Это выбор направления, а не реализация runtime. Принятая 17.10 отделка, приборы, надписи и навигация сохраняются. Новый OpenSpec change пока не создавался: пользователь ещё не выбрал свет.

## Подтверждено исходниками baseline

- `WellArenaPresentation.ConfigureDaylight`: directional .8, rotation (50,-30,0), soft shadows .45, bias .03, normal bias .12.
- `WellArenaPresentation.Build`: flat warm ambient .42; 24 existing ceiling spots, intensity 7, range 12, outer/inner angle 140/100, no spot shadows. Emissive-looking diffusers сами по себе не дают рассеянного GI.
- `NativeVisualPresentation`: default Lighting 50%, fillScale .95, ambientLift .18 дают ambient около (.4995,.4945,.4845); keyScale 1 оставляет daylight .8. Well отсутствует в выборе `localLightPath`: общая Lighting strength не масштабирует его 24 лампы.
- Штатные материалы, AO, Bloom, reflections, color grading включены по умолчанию на 50%. Bloom intensity .15/threshold 1.15; reflection intensity .35, одна cubemap из центра атриума, не отдельное освещение комнат.
- `VisualPolishVolume` содержит Bloom, ColorCurves, ColorAdjustments, без Tonemapping. Экспозиция default 0 EV. `NeutralCurve` с default strength .5 смягчает белое и поднимает тени.
- Project baseline использует Gamma, не Linear. В этой разведке pipeline/color space не мигрируются.
- `WellContactMesh` даёт статическую локальную видимость в цветах вершин: шаг .8 m, reach 2.4 m, 12 samples; WellContactSurface затеняет только indirect. Это уже существующий contact shading, дополненный screen-space AO, а не полное переотражённое освещение.

## Сопоставление сохранённых кадров

Прочитаны `docs/evidence/well-storeys/acceptance.md`, bare `departments/room-floor-0.png`, штатный `full-visual/atrium-corner.png`, `comparisons/final.png` и `full-visual/render-state.txt`. Bare и full-visual используют ConfigureDaylight, но разные камеры: они не являются строгим A/B одного ракурса. В атриуме видны существующие солнечные пятна/тени решёток; в сопоставимых комнатах/лифтовых зонах из final.png свет ровнее, мебель и стены слабо разделяются по объёму. Это визуальная оценка, а не замер освещённости.

Для корректного выбора новый fixture переснимает baseline и все варианты на одинаковых камерах. PNG — исходные Editor renders; JPG boards только масштабируют/собирают их, без обработки яркости или цвета. `compose.py` воспроизводит boards.

## Направления

0 — текущее default освещение; 1 — светлая лаборатория с более выраженными потолочными источниками; 2 — дневное направление/прохладная тень; 3 — локальные тёплые/прохладные островки от существующих приборных ламп. Все варианты сохраняют 24 spots; их тени — эксперимент, стоимость на целевых устройствах не измерялась. Варианты 1–3 также проверяют Neutral tonemapping для светлых поверхностей. Встроенные runtime-профили не изменены.

После выбора рекомендуется standard OpenSpec route: map-local light/effect policy, совместимость старых Lab snapshots и общих graphics controls, сопоставимые Editor before/after. Изменение общего pipeline, увеличение источников, новый декор и изменение карты не входят в согласованный scope. Player/device/performance/audio/human acceptance здесь не выполняются. Full tests, Player build и поставка не заказаны.

## Итог разведки и точка продолжения

Финальный capture: 1/1 Passed, 0 failed/skipped, 28 PNG; XML duration 18.51 s не включает весь startup/import/admission. До него выполнены два технически Passed прохода, которые визуально потребовали коррекции: initial-pass слишком тёмный, bright-pass слишком яркий. Они сохранены отдельно. Финальные 4 boards, atrium-comparison и lifts-comparison осмотрены после записи.

Все три направления остаются кандидатами, без человеческого выбора и реализации. Оценка агента: 2 лучше подчёркивает объём атриума; 3 лучше выражает локальные зоны/цвет этажей; 1 самый ровный и светлый, но имеет наиболее заметный риск ярких пятен. Пиксельная статистика в verification.json учитывает почти белые RGB-пиксели, включая сами лампы; это не доказательство отсутствия пересвета или приёмки.

Ограничения: при включении теней на всех 24 spots штатный 1024 shadow atlas автоматически снижает разрешение. Нельзя переносить такой эксперимент в runtime без отбора теневых источников и affected-проверки. Spot intensity 65–110 выходит за текущий Lab maximum50: это прямые review-only Light значения; диапазоны/профили не изменены. Выбранное направление потребует настройки ярких пятен, сохранения управления эффектами и versioned Lab compatibility. Производительность не измерялась.

Следующий шаг — пользователь выбирает 1, 2 или 3 по показанным изображениям либо даёт конкретную корректировку. После выбора агент фиксирует standard change и реализует только выбранное направление по отдельной команде. Runtime, source assets и main не изменены; проектные настройки, пересохранённые Unity с пробелами, восстановлены в owned worktree.
