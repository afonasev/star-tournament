## 1. Контракты

- [x] 1.1 Сохранить read-only Astra decision memo в design и согласовать scope с GAME_SPEC; strict validation проходит.
- [x] 1.2 Реализовать immutable participant metadata/local-seat mapping/action assembly; EditMode проверяет 2/8,1/4, permutation, invalid mapping/kinds и independent frozen copies.

## 2. Native integration

- [x] 2.1 Разделить N actors и H views/input/HUD, корректные culling layers и session indices; PlayMode проверяет participant7→seat0 и counts1+7/4+4 без fake devices для nonlocal.
- [x] 2.2 Обобщить presentation/corpse/killcam и таблицу до8; tests покрывают nonlocal killer/life, local highlighting, все team totals и palette.
- [x] 2.3 Обобщить initial allocator FFA/teams, bounded budget metadata и atomic failure; physical tests подтверждают8 distinct slots,1/7 teams и bounded impossible failure с nodes/time.
- [x] 2.4 Проверить frozen Repeat/pause/disconnect/menu и переходы4→1→4; lifecycle PlayMode и human setup regressions проходят.

## 3. Приёмка

- [x] 3.1 Пройти полные EditMode/PlayMode/Mac build, сохранить XML/log/build identity.
- [x] 3.2 Выполнить muted focused FHD/4K native journey solo/mixed8/FFA/teams/3seat standings/death/respawn/results/Repeat и обычный keyboard regression; сохранить просмотренные screenshots/state/performance diagnostics.
- [x] 3.3 Получить Astra read-only review, исправить замечания, обновить matrix/AI/roadmap/handoff/evidence, пройти strict/diff checks и создать отдельный commit; полная цель остаётся активной.
