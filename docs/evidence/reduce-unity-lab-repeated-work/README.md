# Сокращение повторной работы Unity Lab

Change `reduce-unity-lab-repeated-work`, session `01a102a0-30cf-7550-a234-661bb883e3c5`.
Baseline: `06877122409c9535cfdf0eae43ffe6f6420862bb`. Implementation: `e13b724`.
Unity 6000.3.23f1, локальный Mac. Пользователь поручил реализацию после расследования длительности тестов.

## Результат bounded warm probes

| Один и тот же поднабор | До | После | Изменение |
| --- | ---: | ---: | ---: |
| 4 migration cases | 99.169764 с | 71.048065 с | −28.36% |
| 3 PlayMode cases | 54.872886 с | 43.245668 с | −21.19% |

XML root durations: baseline EditMode 99.2045437 с (4 cases), after EditMode 71.33907 с (10 cases, включая 6 новых regressions); baseline PlayMode 54.9037524 с, after PlayMode 43.2713349 с. В сравнении EditMode суммируются только одинаковые прежние cases, новые regressions исключены из таблицы, но обязаны пройти. PlayMode сравнивает те же 3 cases. Длительности parent suites не прибавляются к cases.

Это один warm exclusive run на вариант с временной instrumentation. У after варианта дополнительные phase counters; статистическое/target-hardware ускорение не установлено. Fingerprint-прогоны отдельные, их большие JSON transcripts добавляют диагностические затраты и не являются performance comparison или delivery gate. Cold diagnostic имел project startup 42.302 с; startup/import и admission не входят в XML duration. Для after warm EditMode filesystem timestamps показывают ~682.5 с до создания editor log: приблизительная admission задержка с небольшим startup overhead, а не тело теста.

## Подтверждённые hotspots и граница изменения

Полезное различие: generation, validation, копирование snapshots и Editor/admission измеряются отдельно. В baseline четырёх migration cases ProvingProfile.Validate суммарно 9.6881 с; after включая новые небольшие regression cases — 0.6204 с. Генерация исторических схем 25.2266 → 24.0193 с: её алгоритм/ёмкость кеша не меняются. Deep probe 3 music migrations: 42 trusted clones — 2.3539 с, 9 commit clones — 1.0639 с; эти inclusive стадии не суммируются с constructor totals.

Validation теперь строит descriptor index на время одного вызова: сохраняет первый duplicate и исходный порядок diagnostics, не хранит stale mutable metadata. Detached copies копируют serialized scalar/string/value/descriptor данные, не разделяют value/owner indexes. Нормализация null strings соответствует JsonUtility; nullable malformed структуры сохраняют прежний JSON fallback. Disk JSON формат и atomic publication не меняются. Не убирались tests, assertions, natural matches и simulation ticks.

Initial PlayMode scene probes: Lab 0.30–0.36 с, участок arena 1.61–1.92 с, actors 0.02–0.31 с, UI 0.27–0.36 с. Это последовательные участки Start, не полный teardown/start-match профиль. `ProvingArena.Build` повторяет native NavMesh bake. Reuse navigation/scenes в этот патч не входит: требуется отдельное доказательство ownership/geometry/settings/physics контракта.

## Equivalence и regression evidence

`fingerprint-equivalence.json`: одинаковы 7 последовательных generated registry transcripts по 6 уникальным cache keys. Каждый fingerprint охватывает ordered full candidate JSON, включая values/defaults/metadata. Повторяющийся ключ также дал тот же результат. Cache key/capacity и candidate order не менялись.

6 `LabSnapshotCopyTests`: JsonUtility-equivalent shipped bundle/revision copies; detached metadata/values/list; reset cached indexes; null-string normalization и malformed fallback; first duplicate diagnostics; изменение metadata между validation calls; null-entry diagnostics. Все прошли в after warm probe (10/10).

`rejected-zero-test-filter.json`: попытка выбрать параметризованный case строкой с `(0)` получила 0 tests и отвергнута, затем probe выполнен проверенным method filter (3 cases). Zero/skipped/failed/missing XML не засчитываются как green.

Diagnostic raw XML/logs сохранены lossless gzip по подпапкам before/after; summaries и metrics позволяют читать результаты без больших XML output sections. Script/patch artifacts воспроизводят временную instrumentation, которая полностью удалена из shipping runtime перед full gate.

## Область QA

Изменяется shared serialization/data-copy путь, поэтому выбран full scope: `make check` + native Lab smoke. Для локальных UI правок по-прежнему доступен `make check-ui` + affected-screen tests + build/native smoke; focused filters служат iteration evidence. QA policy не изменена.

## Full gate и поставка

`make check` exit 0 на `e13b72494a19996920c29aa02b90309fde7e9303` без instrumentation. EditMode 330/330, 535.4706611 с; PlayMode 199/199, 534.3402584 с; failed/skipped/inconclusive = 0. Все 6 new copy regressions и оба natural terminal matches включены. `STAR_PROVING_BUILD_PASS 487362166`. Это shared gate на общей машине, не A/B performance run.

Exact retained identity: `local-macos-player-candidate/e13b72494a19996920c29aa02b90309fde7e9303/2366840912664b57`.
Player: `/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/lab-copy-e13b7249/StarTournamentProvingGround.app`. Все file checksums копии сверены с `player-manifest.json`; manifest SHA256 `2366840912664b578abb7bdbb795537c9699594290e92aa2312a4a9a16d1c4f8`. Generated build/import settings и сцена сохранены как compressed provenance patches, не интегрированы в source.

Native Lab smoke исходного кандидата прошёл: save/branch/rename/delete/guards/restart; оба Player exit 0, selected revision сохранена и history.json byte-identical после restart. PNG 06-history и 11-restarted-revisions визуально проверены. Это автоматический native smoke, не human acceptance.

До интеграции main продвинулся до 2e3e41c (Veteran League profiles/arena). Совместная ревизия требует повторного full gate и собственного кандидата; исходные A/B measurements остаются привязаны к прежней базе.


## Совместная ревизия после обновления main

Main `2e3e41c` объединён без конфликтов в `7fb05975f902dfebd358a36fb9a74aee83b4dbcf`; изменения Veteran League сохранены. Повторный `make check` exit 0: EditMode 330/330 (537.2298055 с), PlayMode 199/199 (516.9699181 с), failed/skipped/inconclusive = 0. Оба natural terminal matches Passed. Build marker `487367086`. Это корректность совместной версии, не новое A/B измерение.

Финальный review candidate: `local-macos-player-candidate/7fb05975f902dfebd358a36fb9a74aee83b4dbcf/ac73844e05a07a57`. App: `/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/lab-copy-7fb05975/StarTournamentProvingGround.app`. Manifest SHA256 `ac73844e05a07a5752dd8d6845b2a099ec919e765923a892644f31e91909d7b6`, все bytes копии проверены.

`native-integrated/smoke.json`: оба запуска exit 0, muted true, selected revision сохранена, history.json byte-identical после restart. PNG истории до/после визуально просмотрены. Классификация: AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE. Raw logs/XML и PNG сохранены в full-integrated/native-integrated; большие history JSON сохранены lossless gzip.

Сборка ожидала общей admission-очереди после тестов; queue wall time не включён в XML duration и A/B выводы. Generated build assets/settings сохранены в integrated-build-generated.patch.gz и восстановлены в worktree; в source не интегрированы.

Интеграция и фактический cleanup записываются в каноническом `star-tournament-planning/openspec/changes/reduce-unity-lab-repeated-work/delivery.json`. Exact кандидат сохранён для открытой human acceptance. Исходный e13b724 кандидат заменён этим совместным результатом; его manifest и smoke остаются историческими доказательствами.
