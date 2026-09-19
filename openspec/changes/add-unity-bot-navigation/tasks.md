## 1. Контракт

- [x] 1.1 Сохранить матрицу полного переноса с источником, текущим состоянием, оставшейся реализацией и evidence для каждой группы требований; проверить полноту относительно GAME_SPEC/OpenSpec.
- [x] 1.2 Зафиксировать navigation/profile/support/state contracts и Astra memo в design; strict OpenSpec validation проходит.

## 2. Исполнение

- [x] 2.1 Реализовать профиль с metadata и независимый валидируемый snapshot; EditMode покрывает freeze, aliasing и invalid restore.
- [x] 2.2 Реализовать semantic route adapter с complete-path и support guards; PlayMode отклоняет ложный этаж, partial/invalid goal и чужую поверхность.
- [x] 2.3 Реализовать follower/recovery через ordinary actions; PlayMode проверяет оба направления stairs/ramp, lanes, replan и живой blocker без teleport.
- [x] 2.4 Подключить honest navigation development journey к advancing session tick; tests проверяют hidden goals/expiry/life reset и pause/Repeat.

## 3. Приёмка среза

- [x] 3.1 Пройти полный EditMode/PlayMode и Mac Development build, сохранить XML/log/build identity.
- [x] 3.2 Выполнить muted focused native Player journey FHD/4K и обычный Player regression, проверить и сохранить screenshots/JSON и performance diagnostic с явными ограничениями.
- [x] 3.3 Получить read-only Astra review, исправить дефекты, обновить GAME_SPEC/AI/roadmap/matrix/handoff/evidence; strict validation и git diff --check проходят, отдельный commit создан.
