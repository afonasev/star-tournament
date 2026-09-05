## 1. Contracts и profile

- [x] 1.1 Добавить immutable `MatchConfiguration` schema v1, canonical identity, FFA/teams roster validation и unit tests для 2–8 участников, обеих команд и unsupported seat/bot/gamepad inputs.
- [x] 1.2 Расширить `prototype-v1` score-target metadata значениями 3000/1000/20 000/100, повысить profile revision/schema и проверить descriptor coverage, step/range и cross-field tests.
- [x] 1.3 Повысить combat scenario identity и описать local/stationary participant fixtures с anchors/hit volumes; проверить canonical ordering/content hash и отклонение старой identity.
- [x] 1.4 Спроектировать и реализовать participant-centric simulation snapshot v3 с match phase/timer/result/statistics/damage ledger, затем проверить strict parse/serialize/hash/deep-freeze и несовместимость v2.

## 2. Детерминированный match reducer

- [x] 2.1 Перевести movement, collision projection, shotgun state и hit-volume lookup с singular player/targets на canonical participant arrays; существующие movement/combat tests должны сохранить утверждённое поведение.
- [x] 2.2 Добавить ordered damage/death events и atomic damage ledger; unit tests должны доказать exact dealt/received symmetry, один death на life и отсутствие damage мёртвой жизни.
- [x] 2.3 Реализовать fixture death/respawn lifecycle с новой life identity, full health/ammo и исходным anchor; tests должны проверить timing, corpse presentation data и отсутствие movement/attack actions.
- [x] 2.4 Реализовать assists по profile window и дедупликацию attacker/victim/killer; headless tests должны покрыть multiple attackers, границу окна, повторные hits и очистку ledger.
- [x] 2.5 Реализовать kill-chain scoring как cumulative deltas, reset по gap/death и post-five increment; table tests должны покрыть цепочки 1–6+, разрыв и сохранение общего score.
- [x] 2.6 Реализовать match clock, team totals, atomic score-limit evaluation, overtime без ничьи и immutable final result; tests должны покрыть FFA, teams, одновременный tick, time limit, score trigger на последнем tick и выход из overtime.
- [x] 2.7 Обновить playable replay/headless runner для configuration/snapshot v3 и доказать per-tick hash parity при независимом replay, serialize/restore и разных observer cadence.

## 3. Input и projections

- [x] 3.1 Добавить отдельный local `UiActionSnapshot` для held `Tab` и edge `Escape`, не меняя gameplay action schema v2; adapter tests должны проверить key repeat, release, focus/pointer-lock clear и отсутствие gamepad reads.
- [x] 3.2 Создать deterministic standings projector для FFA/teams с stable semantic-id tie-break, team totals и всеми columns; projection tests должны доказать отсутствие мутации и собственных scoring вычислений в UI.
- [x] 3.3 Расширить gameplay UI snapshot timer/overtime/scoreboard/pause/results fields и feedback lifetime; validation/projector tests должны покрыть running, paused, overtime и finished transitions.

## 4. DOM match flow

- [x] 4.1 Реализовать responsive pre-match menu для режима, participant names/colors/teams, duration и nullable target из descriptor metadata; component tests должны блокировать invalid roster/team/ranges и создать exact configuration.
- [x] 4.2 Расширить HUD simulation-derived countdown и `OVERTIME`, сохранив health/crosshair/weapon/ammo; component tests должны проверить formatting и отсутствие wall-clock authority.
- [x] 4.3 Реализовать hold-to-view FFA/team scoreboard с нужными columns/grouping/totals и stable order; component/input integration tests должны проверить press, hold, release, killcam и focus loss.
- [x] 4.4 Заменить generic resume overlay полным pause menu: continue, repeat exact match, fullscreen settings и main menu; tests должны покрыть Escape, pointer-lock/fullscreen loss, retryable resume и честное отсутствие audio consumer.
- [x] 4.5 Реализовать results surface с winner/trigger/final standings и actions repeat/menu; component tests должны проверить FFA/team result и отсутствие UI «ничья».

## 5. Browser session и renderer integration

- [x] 5.1 Ввести single-owner browser shell/session lifecycle `menu → match ↔ pause → results`, гарантировать dispose старой session при repeat/menu и покрыть listener/collision/renderer lifecycle tests.
- [x] 5.2 Обновить renderer для alive/dead/respawn participant projections без gameplay authority и проверить target visibility/corpse cleanup/new-life render unit tests.
- [x] 5.3 Сохранить debug mode с configuration/match/profile/scenario identities и расширить browser smoke evidence для timer, participant stats и phase без console errors.
- [x] 5.4 Расширить performance probe фазами menu, live scoreboard, pause, overtime/results; automated tests должны проверить zero-work settled states и HUD cadence не выше presentation profile.

## 6. Verification и handoff

- [x] 6.1 Выполнить `npm run typecheck`, полный `npm test`, `npm run build`, `git diff --check` и `openspec validate add-basic-match-loop --strict`; сохранить краткие pass/fail counts.
- [x] 6.2 Запустить dev-стенд из этой ветки, подтвердить фактический URL HTTP-запросом и выполнить in-app Browser playtest: pre-match FFA/teams, pointer lock, движение/стрельба, respawn, timer, удержание Tab, Escape/pointer-loss pause, repeat и exit menu без console/WebGL errors.
- [x] 6.3 Принудительно пройти score-limit и time-limit ties, проверить `OVERTIME` и завершение только после единоличного лидера; сохранить DOM/simulation evidence, не заявляя физически непроверенные gamepad/split-screen/bots/online.
- [x] 6.4 Выполнить production-browser performance gate по trigger matrix и зафиксировать hard counters, provisional warnings и unavailable metrics; при regression устранить причину до handoff.
- [x] 6.5 Снять актуальные screenshots dev-стенда для pre-match FFA, teams configuration, running HUD, live scoreboard, pause, overtime и FFA/team results; приложить каждое изменённое состояние к handoff.
- [x] 6.6 Создать/завершить handoff-файл `docs/tasks/completed/02-add-basic-match-loop.md`, сверить scope и чужие изменения, отметить все tasks выполненными и создать один отдельный Git-коммит change.
