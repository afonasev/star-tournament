# Закрытие оставшегося browser backlog

Дата: 2026-09-19. Основание — явное решение пользователя перейти на новую реализацию и тестирование в Unity. Статус остатка работ: CLOSED / CANCELLED / SUPERSEDED. Это не отчёт об успешной поставке.

## Активный список main на момент закрытия

| Объект | Историческое состояние | Решение |
| --- | --- | --- |
| add-local-split-screen-controls | 6/9 задач; 3.1–3.3 не пройдены | Архив без spec sync и без изменения checkbox |
| 11-add-game-design-lab | Handoff с требованиями приёмки | Остаток работ снят, handoff в docs/tasks/cancelled |
| 12-add-local-light-shadows | Записаны автопроверки; free-camera physical acceptance открыта | Остаток приёмки снят, исторические результаты сохранены |
| 13-repair-snapshot-profile-bootstrap | Записаны автопроверки и visual smoke; movement не доказан | Остаток handoff снят, исторические результаты сохранены |
| 14-player-wall-clearance | Handoff с collision/visual acceptance | Остаток работ снят, handoff в docs/tasks/cancelled |

## Сохранённая старая ветка

`codex/add-robot-shadow-casters`, HEAD `4b03349`, worktree `/Users/eaafonasev/.codex/worktrees/3863/star-tournament-robot-shadows`.

На этой устаревшей ветке видны add-robot-shadow-casters (scaffold без tasks), expand-procedural-arena-variety (16/17) и add-local-light-shadows (7/7). Это branch-local историческое состояние, не дополнительные действующие обязательства main. Всё оставшееся browser-развитие на этой ветке снято с исполнения; merge/deploy кандидата не требуется. Worktree и коммиты сохранены без удаления/изменения; записи уже завершённых main archives не переписываются.

## Граница решения

- Не дописывать browser-версию ради закрытия старых задач; не выполнять её отложенные gates.
- Не отмечать непройденные проверки успешными; существующие evidence остаются историческими.
- Не синхронизировать отменённые browser delta specs как утверждённую новую реализацию.
- Не удалять код, ассеты, результаты и ветки только ради очистки списка задач.
- GAME_SPEC сохраняет продуктовые требования; Unity получает новую реализацию и тесты с использованием готовых средств движка.
- Этот реестр закрывает legacy browser backlog и не отменяет активную задачу перехода на Unity.
