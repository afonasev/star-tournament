# 01 — First-person combat slice

## Статус

Реализация и автоматические проверки завершены в ветке `change/add-first-person-combat-slice`. OpenSpec change: `add-first-person-combat-slice`.

## Что входит

- single-seat keyboard/mouse action adapter и pointer-lock lifecycle;
- deterministic movement через collision port и Rapier KCC adapter;
- first-person camera/renderer и compact HUD;
- deterministic double-barrel hitscan combat с zoned target fixtures;
- playable snapshot/replay contracts и profile descriptors.

## Проверка

Подробный результат хранится в `openspec/changes/add-first-person-combat-slice/verification.md`.

Пользователь подтвердил физические ходьбу, прыжок и стрельбу. Незакрытый gate сузился до явного подтверждения mouse look, hit/destroy и lock loss/resume. До этой проверки задачу не переносить в `docs/tasks/completed/`, OpenSpec change не архивировать и ветку не интегрировать в `main` как полностью принятую.

Отдельный обязательный follow-up: устранить обнаруженный быстрый нагрев и ввести постоянный performance/power gate до следующего критичного расширения renderer, split-screen или контента.
