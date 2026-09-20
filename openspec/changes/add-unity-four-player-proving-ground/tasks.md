## 1. Основа и эталон

- [x] 1.1 Зафиксировать stage-0 inventory, browser commit/profile/seed/scenario и сравнение кандидатов; сверить первичные источники и новые решения в GAME_SPEC/roadmap/native QA.
- [x] 1.2 Создать изолированный Unity-проект с pinned packages и lock; подтвердить успешный import/compile в 6000.3.23f1 без изменения browser runtime.

## 2. Проверочный полигон

- [x] 2.1 Реализовать профиль/descriptor registry, сериализуемое состояние и CharacterController motor; EditMode/PlayMode проверяют validation, headroom, wall/contact и оба направления stairs/ramp.
- [x] 2.2 Создать общую authored arena fixture и AI Navigation route probe; проверить отсутствие ложной межэтажной связи, корректный route через оба transitions и отсутствие второго владельца Transform.
- [x] 2.3 Реализовать четыре local seats, Input System bindings, cameras/uGUI, setup/pause/focus/reconnect; automated input проверяет изоляцию устройств и очистку held input.
- [x] 2.4 Импортировать существующие robot/shotgun GLB через manifest, проверить scale/orientation/materials/mounts и shader availability в native Player; diagnostic shot probe проверяет aim и query semantics.

## 3. Проверки и передача

- [x] 3.1 Выполнить интеграционные EditMode/PlayMode tests и Mac Player build; сохранить результаты, package/build/profile identities и команды воспроизведения.
- [ ] 3.2 Провести muted native Player playtest со скриншотами setup/live/pause, измерить Full HD/4K и 1/2/4 cameras; отсутствующий GPU timing и предварительный hardware статус указать явно. Browser QA не применим к native-only change по GAME_SPEC §8.
- [ ] 3.3 Пройти physical acceptance четырьмя реальными назначенными устройствами и TV/focus/reconnect; подтвердить performance на согласованном reference hardware/quality, не подменять синтетическим прогоном.
- [x] 3.4 Сохранить handoff с результатом сравнения/ограничениями, выполнить strict OpenSpec validation и отдельный commit; integration/archive только после обязательной приёмки.

Примечание к 3.2: native setup/live/pause smoke и 6 diagnostic ячеек выполнены; полноценный четырёхместный Player playtest, foreground timing и привязка асинхронных CPU/GPU samples остаются открытыми. См. `docs/tasks/15-unity-four-player-proving-ground.md`.
