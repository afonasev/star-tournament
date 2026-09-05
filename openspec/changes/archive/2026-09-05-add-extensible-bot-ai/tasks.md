## 1. Данные и общие игровые правила

- [x] 1.1 Добавить bot descriptor/difficulty и explicit browser/headless validation; проверить 2–8 roster, неверные difficulty и zero-local harness tests.
- [x] 1.2 Добавить profile AI groups и emptyRefillAmmo со всеми metadata; проверить numeric coverage, cross-field validation и новые identity/hash fixtures.
- [x] 1.3 Реализовать пополнение при нуле для всех участников; проверить 1 → 20, zero-state normalization, held fire и неизменный cooldown.
- [x] 1.4 Обобщить movement/combat tick и dynamic hit volumes на всех участников; проверить mutual kill, canonical damage, alive collision, local death/respawn и отсутствие двойного scoring.

## 2. AI и расширяемость

- [x] 2.1 Реализовать observation boundary, LOS/FOV, timestamped memory и сообщения союзников; тест скрытого перемещения должен сохранять одинаковые решения при одинаковых доступных наблюдениях.
- [x] 2.2 Добавить сериализуемые BotState/RNG и версии replay/snapshot; проверить roundtrip, incompatible versions и продолжение из нового collision world.
- [x] 2.3 Реализовать graph navigation, clearance refinement и stuck recovery; проверить реальный обход walls/panels, двери и уступание живым capsules.
- [x] 2.4 Реализовать выбор намерений, aim/weapon policy, strafe и ограниченные прыжки; fixtures подтверждают активный поиск, grounded интервалы, bounded retreat и отсутствие мгновенного наведения.
- [x] 2.5 Реализовать временную поддержку и separation; scenarios показывают помощь человеку/боту, распад группы и отсутствие координации в FFA.
- [x] 2.6 Добавить contract tests расширения goal/weapon capabilities тестовыми реализациями; проверить отсутствие AI imports из DOM/renderer и сохранение общих combat rules.

## 3. Runtime и интерфейс

- [x] 3.1 Перевести setup на редактируемый roster с add/remove и индивидуальной сложностью; component tests подтверждают stable ids, независимость полей, лимит и team validation.
- [x] 3.2 Добавить общий display formatter для live/results/winner label и Repeat; проверить три русские подписи, длинные имена и отсутствие двойных суффиксов.
- [x] 3.3 Подключить AI в browser tick и presentation всех движущихся участников; runtime tests подтверждают shooter-specific animation/events, pause/hidden/finished zero-work и неизменный single viewport при подключении gamepad.

## 4. Ускоренная оценка

- [x] 4.1 Добавить ai:simulate/ai:evaluate на shared playable engine с реальным collision; smoke запускает bot-only FFA/teams без DOM/WebGL и выводит wall time, ticks и ускорение.
- [x] 4.2 Добавить telemetry, timeout/stall outcomes и воспроизводимые replay bundles; искусственно созданная ошибка повторяется с теми же hashes.
- [x] 4.3 Зафиксировать ai-evaluation-v1, независимые seeds и behavior thresholds; артефакт suite описывает paired sides/identity, sample size и fail/inconclusive критерии до tuning.
- [x] 4.4 Настроить три профиля на calibration seeds и выполнить независимую оценку; отчёт подтверждает рост силы с доверительным интервалом, отсутствие timeout/stall и проходит navigation/jump/cooperation scenarios.
- [x] 4.5 Проверить repeated runs, snapshot restore и browser/headless hash parity; сохранить итоговые identities/hashes и failure artifacts при расхождении.

## 5. Приёмка и handoff

- [x] 5.1 Выполнить typecheck, полный test, build и strict OpenSpec validation; сохранить краткую сводку результатов.
- [x] 5.2 Запустить dev из текущего worktree, проверить HTTP и сообщить фактический URL; muted in-app Browser проверяет add/remove, смешанную сложность, FFA/teams, physical move/look/jump/fire, live/results, killcam/respawn, Repeat и pause/resume.
- [ ] 5.3 Сохранить и визуально проверить PNG setup, игрового боя, live и результатов для FFA/teams; приложить absolute-path images с seed/profile/revision к handoff.
- [x] 5.4 Выполнить collision/performance gates с AI и восемью участниками; сохранить timings/hashes/zero-work evidence, а physical-input блокировку явно оставить незакрытой до устойчивой browser сессии.
- [ ] 5.5 Обновить GAME_SPEC о фактической реализации, сводку handoff и завершённую task file; проверить git diff, выполнить отдельный feature commit, затем по delivery workflow последовательную интеграцию, OpenSpec archive и post-deploy проверку актуального main.

### Приёмочный остаток

- 5.2: автоматизация не получила pointer lock. Пользователь затем явно подтвердил, что провёл оставшуюся ручную AI-приёмку (move/look/jump/fire и pause/resume); пункт закрыт по его проверке вместе с ранее сохранёнными browser evidence.
- 5.3: сохранены и показаны PNG setup, team setup/live и FFA/team results; отдельного physical combat/killcam PNG нет. Полного physical playtest эти изображения не заменяют.
- 5.4: восьмисторонний browser performance reference PASS (simulation p95 до 1.6 ms, около 60 ticks/s), settled phases zero-work; physical часть подтверждена ручной проверкой пользователя.
- 5.5: feature commit `c9d51e8` интегрирован в main; спецификации синхронизированы и change архивирован коммитом `a4bcb64`, включённым в main. Деплой и post-deploy проверка не выполнены: remote и deployment configuration отсутствуют.

### Согласованное закрытие с предупреждениями — 2026-09-05

Пользователь принял результат («все ок»), затем явно подтвердил («да») синхронизацию спецификаций, интеграцию в main и архивирование с указанными ограничениями browser-проверки и открытым деплоем. На момент архивации пункты 5.2–5.5 были оставлены неполными: согласие на закрытие не является доказательством каждого физического сценария или публикации. Требования будущей browser-приёмки не изменены. Handoff переносится в completed как принятый с исключениями; остаток хранится здесь и в verification.md, без создания новой feature-задачи.


### Уточнение ручной приёмки

После архивации пользователь сообщил, что провёл оговорённый остаток AI-приёмки. Пункты 5.2 и 5.4 закрыты с явной атрибуцией ручной проверки пользователю. Пункты 5.3 (PNG) и 5.5 (деплой) остаются неполными; новые изображения и публикация не заявляются.
