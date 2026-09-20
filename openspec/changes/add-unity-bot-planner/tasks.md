## 1. Contracts and tuning

- [x] 1.1 Сохранить Astra memo/решения в design, обновить GAME_SPEC и пройти strict validation.
- [x] 1.2 Добавить behavior profile/metadata и pure planner contracts/state; EditMode проверяет ranges, independent snapshots, atomic invalid restore и одинаковый observation stream.

## 2. Actual bot behavior

- [x] 2.1 Реализовать target/reaction/aim/fire policy и search/engage decisions; directed tests доказывают честность, target life, три сложности и real release ticks.
- [x] 2.2 Реализовать tactical steering/jump/retreat/support и navigation arbitration; actual physics tests покрывают support/headroom/transition/recovery, bounded support/retreat и human ally.
- [x] 2.3 Подключить match driver и frozen lifecycle; PlayMode доказывает реальные AI shots/damage/death/respawn, pause/Repeat и human routing в mixed8.

## 3. Acceptance

- [x] 3.1 Полные EditMode/PlayMode/Mac build проходят; XML/log/build identity сохранены.
- [x] 3.2 Выполнить focused muted native FHD/4K journey actual AI FFA/teams/1–4views, keyboard-versus-bots и lifecycle; сохранить screenshots/metrics, явно отделить pilot от difficulty/performance acceptance.
- [x] 3.3 Astra read-only review, fixes, docs/matrix/handoff/evidence/vault, strict/diff checks и отдельный commit; продолжить shipping setup, не закрывая full migration.
