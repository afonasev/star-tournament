## 1. Данные и общие игровые правила

- [ ] 1.1 Добавить bot descriptor/difficulty и explicit browser/headless validation; проверить 2–8 roster, неверные difficulty и zero-local harness tests.
- [ ] 1.2 Добавить profile AI groups и emptyRefillAmmo со всеми metadata; проверить numeric coverage, cross-field validation и новые identity/hash fixtures.
- [ ] 1.3 Реализовать пополнение при нуле для всех участников; проверить 1 → 20, zero-state normalization, held fire и неизменный cooldown.
- [ ] 1.4 Обобщить movement/combat tick и dynamic hit volumes на всех участников; проверить mutual kill, canonical damage, alive collision, local death/respawn и отсутствие двойного scoring.

## 2. AI и расширяемость

- [ ] 2.1 Реализовать observation boundary, LOS/FOV, timestamped memory и сообщения союзников; тест скрытого перемещения должен сохранять одинаковые решения при одинаковых доступных наблюдениях.
- [ ] 2.2 Добавить сериализуемые BotState/RNG и версии replay/snapshot; проверить roundtrip, incompatible versions и продолжение из нового collision world.
- [ ] 2.3 Реализовать graph navigation, clearance refinement и stuck recovery; проверить реальный обход walls/panels, двери и уступание живым capsules.
- [ ] 2.4 Реализовать выбор намерений, aim/weapon policy, strafe и ограниченные прыжки; fixtures подтверждают активный поиск, grounded интервалы, bounded retreat и отсутствие мгновенного наведения.
- [ ] 2.5 Реализовать временную поддержку и separation; scenarios показывают помощь человеку/боту, распад группы и отсутствие координации в FFA.
- [ ] 2.6 Добавить contract tests расширения goal/weapon capabilities тестовыми реализациями; проверить отсутствие AI imports из DOM/renderer и сохранение общих combat rules.

## 3. Runtime и интерфейс

- [ ] 3.1 Перевести setup на редактируемый roster с add/remove и индивидуальной сложностью; component tests подтверждают stable ids, независимость полей, лимит и team validation.
- [ ] 3.2 Добавить общий display formatter для live/results/winner label и Repeat; проверить три русские подписи, длинные имена и отсутствие двойных суффиксов.
- [ ] 3.3 Подключить AI в browser tick и presentation всех движущихся участников; runtime tests подтверждают shooter-specific animation/events, pause/hidden/finished zero-work и неизменный single viewport при подключении gamepad.

## 4. Ускоренная оценка

- [ ] 4.1 Добавить ai:simulate/ai:evaluate на shared playable engine с реальным collision; smoke запускает bot-only FFA/teams без DOM/WebGL и выводит wall time, ticks и ускорение.
- [ ] 4.2 Добавить telemetry, timeout/stall outcomes и воспроизводимые replay bundles; искусственно созданная ошибка повторяется с теми же hashes.
- [ ] 4.3 Зафиксировать ai-evaluation-v1, независимые seeds и behavior thresholds; артефакт suite описывает paired sides/identity, sample size и fail/inconclusive критерии до tuning.
- [ ] 4.4 Настроить три профиля на calibration seeds и выполнить независимую оценку; отчёт подтверждает рост силы с доверительным интервалом, отсутствие timeout/stall и проходит navigation/jump/cooperation scenarios.
- [ ] 4.5 Проверить repeated runs, snapshot restore и browser/headless hash parity; сохранить итоговые identities/hashes и failure artifacts при расхождении.

## 5. Приёмка и handoff

- [ ] 5.1 Выполнить typecheck, полный test, build и strict OpenSpec validation; сохранить краткую сводку результатов.
- [ ] 5.2 Запустить dev из текущего worktree, проверить HTTP и сообщить фактический URL; muted in-app Browser проверяет add/remove, смешанную сложность, FFA/teams, physical move/look/jump/fire, live/results, killcam/respawn, Repeat и pause/resume.
- [ ] 5.3 Сохранить и визуально проверить PNG setup, игрового боя, live и результатов для FFA/teams; приложить absolute-path images с seed/profile/revision к handoff.
- [ ] 5.4 Выполнить collision/performance gates с AI и восемью участниками; сохранить timings/hashes/zero-work evidence, а physical-input блокировку явно оставить незакрытой до устойчивой browser сессии.
- [ ] 5.5 Обновить GAME_SPEC о фактической реализации, сводку handoff и завершённую task file; проверить git diff, выполнить отдельный feature commit, затем по delivery workflow последовательную интеграцию, OpenSpec archive и post-deploy проверку актуального main.
