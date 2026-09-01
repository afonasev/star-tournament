# 01 — First-person combat slice

## Статус

Реализация, автоматические проверки и физическая browser-приёмка завершены в ветке `change/add-first-person-combat-slice`. OpenSpec change: `add-first-person-combat-slice`.

## Что входит

- single-seat keyboard/mouse action adapter и pointer-lock lifecycle;
- deterministic movement через collision port и Rapier KCC adapter;
- first-person camera/renderer и compact HUD;
- deterministic double-barrel hitscan combat с zoned target fixtures;
- playable snapshot/replay contracts и profile descriptors.

## Проверка

Подробный результат хранится в `openspec/changes/add-first-person-combat-slice/verification.md`.

Пользователь подтвердил физические ходьбу, прыжок, стрельбу, mouse look, hit/destroy и `Esc` → `Продолжить`. Physical keyboard/mouse gate закрыт.

Постоянный performance/power gate введён и проходит portable budgets. После стабилизации пользователь подтвердил, что заметный нагрев ушёл и вентиляторы перестали работать на высокой скорости. Универсальные thermal budgets для других устройств остаются будущей работой после утверждения reference hardware.
