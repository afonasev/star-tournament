# F8 — финальная диагностика

Точный Player: `exact-player-manifest.json`, source `f377625`. 3456×2160 fullscreen, VSync1/120Hz, seed20261003, одна камера, 8 hard bots. Все 90 источников сохранены. Raw CSV/PNG и невалидные samples сохранены без обрезания.

## Парные frozen виды

| Вид | mean baseline → candidate, мс | p99 baseline → candidate, мс | Δ mean |
| --- | ---: | ---: | ---: |
| yard | 11.36 → 11.48 | 20.58 → 17.34 | -1.0% |
| hall | 21.85 → 15.56 | 30.16 → 24.86 | 28.8% |
| north-lower | 21.40 → 15.15 | 28.85 → 24.39 | 29.2% |
| upper | 19.14 → 18.64 | 26.01 → 30.32 | 2.6% |
| lab-a | 20.50 → 17.41 | 33.25 → 26.97 | 15.1% |
| lab-b | 20.84 → 9.08 | 33.43 → 16.80 | 56.4% |
| west-stair | 9.74 → 8.87 | 25.36 → 16.93 | 9.0% |
| east-stair | 9.08 → 8.63 | 20.04 → 16.82 | 5.0% |
| west-opening | 9.04 → 8.91 | 17.05 → 17.08 | 1.5% |
| east-opening | 8.75 → 8.69 | 16.81 → 17.02 | 0.8% |
| north-door | 9.18 → 8.62 | 17.24 → 16.67 | 6.1% |
| south-door | 14.01 → 9.49 | 21.28 → 16.85 | 32.3% |
| west-stair-low | 10.70 → 9.13 | 23.28 → 21.24 | 14.6% |
| east-stair-low | 9.37 → 8.93 | 23.96 → 17.41 | 4.7% |

У всех выбранных frozen пар совпадают pose, FOV, resolution, quality, renderScale, cameras и matchTime. State совпадает побитно кроме south-door: float settling Position.y различается на 5.96e-8 м, Velocity.y на 2.98e-6 м/с; числовой допуск анализатора1e-5, raw states сохранены. Верхний зал и Lab A используют единственный разрешённый повтор после потери фокуса. Lab B — сравнение между разными запусками, уверенность ниже. Небольшое изменение двора и верхнего зала нельзя трактовать как надёжную экономию.

## Все samples, включая потерянный фокус

| Run / sample | Focus | mean | p95 | p99 | max | >16.67 / >25 / >50 мс |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| exact-frozen / yard-baseline | 705/705 | 11.36 | 16.88 | 20.58 | 38.06 | 48 / 4 / 0 |
| exact-frozen / yard-candidate | 698/698 | 11.48 | 16.66 | 17.34 | 29.11 | 33 / 1 / 0 |
| exact-frozen / hall-baseline | 367/367 | 21.85 | 29.01 | 30.16 | 43.81 | 300 / 103 / 0 |
| exact-frozen / hall-candidate | 515/515 | 15.56 | 20.95 | 24.86 | 29.88 | 255 / 3 / 0 |
| exact-frozen / north-lower-baseline | 375/375 | 21.40 | 25.89 | 28.85 | 35.04 | 286 / 109 / 0 |
| exact-frozen / north-lower-candidate | 529/529 | 15.15 | 20.51 | 24.39 | 25.15 | 228 / 1 / 0 |
| exact-frozen / upper-baseline | 0/482 | 16.61 | 21.53 | 25.63 | 28.96 | 238 / 5 / 0 |
| exact-frozen / upper-candidate | 0/819 | 9.79 | 14.87 | 16.53 | 41.69 | 8 / 2 / 0 |
| exact-frozen / lab-a-baseline | 0/604 | 13.26 | 17.88 | 19.41 | 29.43 | 68 / 1 / 0 |
| exact-frozen / lab-a-candidate | 0/923 | 8.67 | 10.86 | 13.58 | 21.64 | 5 / 0 / 0 |
| exact-frozen / lab-b-baseline | 528/535 | 14.97 | 18.45 | 21.36 | 44.00 | 117 / 2 / 0 |
| exact-frozen / lab-b-candidate | 882/882 | 9.08 | 16.35 | 16.80 | 26.49 | 14 / 1 / 0 |
| exact-frozen / west-stair-baseline | 822/822 | 9.74 | 16.85 | 25.36 | 33.99 | 57 / 15 / 0 |
| exact-frozen / west-stair-candidate | 903/903 | 8.87 | 16.30 | 16.93 | 25.87 | 25 / 2 / 0 |
| exact-frozen / east-stair-baseline | 882/882 | 9.08 | 16.41 | 20.04 | 29.72 | 31 / 4 / 0 |
| exact-frozen / east-stair-candidate | 928/928 | 8.63 | 8.68 | 16.82 | 25.47 | 14 / 1 / 0 |
| exact-frozen / west-opening-baseline | 885/885 | 9.04 | 14.00 | 17.05 | 33.60 | 19 / 2 / 0 |
| exact-frozen / west-opening-candidate | 899/899 | 8.91 | 16.10 | 17.08 | 25.42 | 27 / 5 / 0 |
| exact-frozen / east-opening-baseline | 915/915 | 8.75 | 9.28 | 16.81 | 25.27 | 19 / 2 / 0 |
| exact-frozen / east-opening-candidate | 922/922 | 8.69 | 8.85 | 17.02 | 25.54 | 17 / 1 / 0 |
| exact-frozen / north-door-baseline | 872/872 | 9.18 | 16.49 | 17.24 | 25.03 | 35 / 1 / 0 |
| exact-frozen / north-door-candidate | 929/929 | 8.62 | 8.77 | 16.67 | 25.17 | 9 / 1 / 0 |
| exact-frozen / south-door-baseline | 571/571 | 14.01 | 17.81 | 21.28 | 27.72 | 67 / 2 / 0 |
| exact-frozen / south-door-candidate | 844/844 | 9.49 | 14.41 | 16.85 | 24.92 | 15 / 0 / 0 |
| exact-frozen / west-stair-low-baseline | 749/749 | 10.70 | 16.36 | 23.28 | 27.20 | 28 / 3 / 0 |
| exact-frozen / west-stair-low-candidate | 877/877 | 9.13 | 16.42 | 21.24 | 33.56 | 32 / 6 / 0 |
| exact-frozen / east-stair-low-baseline | 855/855 | 9.37 | 16.25 | 23.96 | 33.47 | 39 / 6 / 0 |
| exact-frozen / east-stair-low-candidate | 897/897 | 8.93 | 15.80 | 17.41 | 25.50 | 21 / 3 / 0 |
| exact-frozen / group-upper-baseline | 580/580 | 13.81 | 17.44 | 19.90 | 26.71 | 52 / 1 / 0 |
| exact-frozen / group-upper-lower-point | 806/806 | 9.94 | 13.90 | 16.34 | 21.81 | 5 / 0 / 0 |
| exact-frozen / group-upper-lower-spot | 573/573 | 13.96 | 17.66 | 19.47 | 29.16 | 78 / 1 / 0 |
| exact-frozen / group-upper-upper-point | 729/729 | 10.99 | 15.75 | 17.50 | 25.26 | 13 / 2 / 0 |
| exact-frozen / group-upper-upper-spot | 661/661 | 12.10 | 16.02 | 17.38 | 27.21 | 18 / 1 / 0 |
| exact-frozen / group-upper-stairs-point | 572/572 | 13.99 | 17.64 | 19.10 | 28.32 | 74 / 1 / 0 |
| exact-frozen / group-upper-stairs-spot | 464/464 | 17.26 | 23.81 | 27.84 | 40.45 | 262 / 15 / 0 |
| exact-frozen / live-candidate | 6350/6350 | 9.45 | 15.90 | 18.94 | 33.30 | 230 / 18 / 0 |
| exact-focus-repeat / upper-baseline | 419/419 | 19.14 | 25.20 | 26.01 | 26.37 | 259 / 64 / 0 |
| exact-focus-repeat / upper-candidate | 430/430 | 18.64 | 25.12 | 30.32 | 50.70 | 272 / 47 / 1 |
| exact-focus-repeat / lab-a-baseline | 391/391 | 20.50 | 26.30 | 33.25 | 35.21 | 285 / 90 / 0 |
| exact-focus-repeat / lab-a-candidate | 460/460 | 17.41 | 25.05 | 26.97 | 28.09 | 269 / 26 / 0 |
| exact-focus-repeat / lab-b-baseline | 384/384 | 20.84 | 29.26 | 33.43 | 33.57 | 284 / 96 / 0 |
| exact-live-baseline / live-baseline | 3417/3417 | 17.56 | 28.58 | 33.66 | 47.04 | 1640 / 425 / 0 |

## Вывод и ограничения

Нижний зал: 21.85→15.56 мс в этом прогоне (≈46→64 FPS), ранее валидно 17.89→11.30 мс. Live baseline→candidate: 17.56→9.45 мс (≈57→106 FPS); p99 33.66→18.94 мс. Live стартует с разным simulation time и не является identical-state pair. Все хвосты сохранены: верхний candidate в повторе имеет max 50.70 мс и один кадр >50 мс. Стабильные 120 FPS не доказаны.

Валидные верхние группы: baseline13.81 мс; без lower-point9.94, lower-spot13.96, upper-point10.99, upper-spot12.10, stairs-point13.99, stairs-spot17.26. Это отдельные последовательные samples, не аддитивный budget; рост последнего sample также сохранён. Старые CPU tails и потеря фокуса остаются в прежних reports и не заменяются этой серией.

Просмотр агента: `final-visible/floors.png`, `stairs.png`, `openings.png`, `native-f7-f8.png`; исходные PNG рядом. Яркий офисный вид, геометрия, материалы и читаемость сохранены; местами локальные блики сильнее. Это агентская visual проверка, не human acceptance.

Natural match PASS: seed20261003, 149 kills, 136 collections, muted. Полный автоматический gate ранее PASS334 EditMode +201 PlayMode, zero skipped/failed; exact build и весь exclusive QA batch exit0. Human visual/game acceptance открыта; archive запрещён.

Полные CPU/GPU/main/render/present-wait/draw/setpass distributions и счётчики доступны в `final-summary.json` и исходных `performance.json`. GPU FrameTiming не используется как доказательство 8.33-мс budget.
