## 1. Контракты профиля и документация

- [ ] 1.1 Зафиксировать в `docs/GAME_SPEC.md` доступ из главного меню, Continue/Discard и правило «только следующий матч»; проверить diff против разделов 5–6.
- [ ] 1.2 Реализовать versioned profile repository: отдельные append-only histories по profile id в `.local/`, checked-in `balance/releases.json`, selected/release reference, parser/validation/hash mismatch errors; проверить unit tests для save/load/corruption/immutable baseline.
- [ ] 1.3 Добавить dev-only Vite endpoint для atomic publish/release promotion с same-origin и optimistic-version checks; проверить unit tests для immutable published snapshot и concurrent-write conflict.
- [ ] 1.4 Добавить active handoff в `docs/tasks/` с границами change и acceptance evidence; проверить свободный префикс и отсутствие посторонних файлов в diff.

## 2. DOM Game Design Lab

- [ ] 2.1 Реализовать полноэкранный menu route и descriptor-driven группы/поля без technical invariants и presentation profile; проверить component tests на coverage и metadata.
- [ ] 2.2 Реализовать draft editing, inline validation, Save новой immutable revision и отдельный ordered Changelog; проверить component/unit tests для range, cross-field errors, delta и неизменности base.
- [ ] 2.3 Реализовать dirty-close confirmation Continue/Discard, независимый выбор profile/revision и действие «Сделать релизной версией»; проверить component tests, что draft не создаёт match configuration.

## 3. Интеграция нового матча

- [ ] 3.1 Передать exact выбранный valid profile либо repository release profile в следующий match setup/bootstrap и показать identity/hash/release state в меню; проверить integration tests для selected profile и ошибки отсутствующей/повреждённой revision без silent fallback.
- [ ] 3.2 Сохранить pause/current-match boundary: не добавлять Lab action в pause и не менять profile/arena/snapshot/replay активного матча; проверить regression tests.

## 4. Проверка и handoff

- [ ] 4.1 Запустить `npm test`, `npm run build`, `openspec validate add-game-design-lab --strict` и `git diff --check`; зафиксировать краткие результаты.
- [ ] 4.2 Запустить dev-стенд из worktree с muted audio, проверить HTTP availability и провести in-app Browser playtest: baseline, metadata, invalid draft, changelog, dirty close, save/select и новый матч; сохранить и визуально проверить PNG каждого изменённого состояния.
- [ ] 4.3 Закоммитить только change в отдельный commit и обновить handoff с фактическим URL, revision, тестами и screenshot evidence.
