# Ускорение Unity checks

Авторизованный scope: focused CLI checks, профилирование historical Lab и длинных bot PlayMode сценариев; оптимизация доказанной повторной работы без изменения баланса, hashes или simulated-match contract. Change: `speed-up-unity-checks`, shared store `star-tournament-planning`.

## Условия измерения

- Исходный runtime: `11576afa6929d411568bb75bbf57dd3b94fd64ef`, собственный worktree `unity-checks`, Unity 6000.3.23f1, Test Framework 1.6.0, macOS на текущем Mac.
- Все timed runs используют `UNITY_RUN_MODE=exclusive unity/tools.sh ...`. Queue, import/compilation и editor startup не входят в XML test duration. Это локальная диагностика, не target-hardware performance acceptance.
- Baseline full gates выполняются без instrumentation. `profile-before`/`profile-after` содержат временные timing/fingerprint patches, они не являются delivery gates. Final full gates выполняются после удаления instrumentation.
- Нет сокращения 300 секунд / 15000 ticks естественных матчей; все прежние тесты сохранены.

## Измеренные hotspots

`profile-before/summary.json` и raw XML/logs содержат подробные данные.

- Historical generation: 16 cache misses в исходном fixture; сравнение схем суммарно 20.14 с, registry keys 6.51 с. Первичные transforms и additive pickup branch также выполняют serialization. Memoization теперь повторно использует immutable profile schema fragments/distances и pickup predecessors. Cache key по-прежнему включает exact shipped values/metadata и target schema; cache capacity не увеличена.
- Migration value copy: 2.37–2.52 с в трёх длинных сценариях. Линейная проверка наличия пути для каждого старого descriptor заменена единственным trusted descriptor index. Повторный hash внутри поиска существующей ревизии вычисляется один раз.
- Bot loop: assertions около 0.5 с, driver 6.7–7.6 с; session tick 11–15.8 с. В session наиболее дорогая часть — rifle bullet processing (7.4–8.4 с) и weapon processing (1.6–4.1 с). Movement/sync/respawn существенно меньше. Эти времена inclusive: содержат physics и presentation callbacks, не только core math.
- Отдельный resolver probe: 3,539,250 zone lookups, 695.961 мс; 25,963 segment resolutions, 861.637 мс (instrumented). Размеры hit-зон вычисляются один раз из private frozen profile, с теми же float products. Physics, event callbacks, cadence, RNG, target ordering и strict-distance comparisons не меняются.

## Быстрый путь

```sh
unity/tools.sh test-edit-filter 'DesignLabHistoryTests'
unity/tools.sh test-play-filter 'NativeBotBehaviorTests.NaturalDefaultEight'
```

Каждый focused запуск сохраняет собственные XML/logs и `scope.txt`; не переписывает `editmode.xml`/`playmode.xml`. Проверяйте test count. Focused green не заменяет полный `unity/tools.sh test-edit`, `test-play` и `build` перед интеграцией.

## Ограничения

Значительное ускорение bot матчей заранее не обещано: оптимизация lookup относится к меньшей части их стоимости. Не убирались physical queries, presentation или assertions ради времени теста. Приёмка человеком и physical controller / TV / target hardware gates не выводятся из автоматизированных результатов.

## Итоговые полные gates без instrumentation

| Suite | Исходный runtime 11576af | Готовый runtime b42da2c | Результат |
| --- | --- | --- | --- |
| EditMode | 239/239, 210.12 с | 243/243, 132.59 с | На 36.9% быстрее с дополнительными regressions |
| PlayMode | 161/161, 410.50 с | 161/161, 426.03 с | В этом прогоне на 3.8% дольше; ускорение не подтверждено |
| NativeBotBehaviorTests | 96.84 с | 100.47 с | Контракт и outcome counters сохранены; wall-time выигрыш не подтвержден |

Старые 32 Lab cases: 199.96 → 108.38 с (−45.8%); новый metadata/cache isolation case добавляет 15.30 с, весь новый Lab fixture — 123.69 с. Новые boundary/frozen-dimensions cases также вошли в полный EditMode.
Отдельный diagnostic transcript 6000 rays: 76.844 → 18.403 мс с одинаковым fingerprint. Это короткий micro-scenario, не оценка всего матча.
Один полный run на вариант, без статистического claim. Test duration исключает startup/import/queue; полный пользовательский wall time будет больше.

Exact code commit: `b42da2c64ac2a54512f3f7f7192b1161aede04be`; Unity Assets source tree `0d98f0babcad416e72b942c8e88de066d7206b2f`. Последующие evidence-only commits не меняют tested runtime.

## Native build / smoke / retained candidate

Build exit 0, `STAR_PROVING_BUILD_PASS 471333920`. Exact retained identity: `local-macos-player-candidate/b42da2c64ac2a54512f3f7f7192b1161aede04be/81be58ec11b14715`.
App: `/Users/eaafonasev/Projects/star-tournament/.local/native-candidates/speed-up-unity-checks/b42da2c64ac2a54512f3f7f7192b1161aede04be/StarTournamentProvingGround.app`. `full-final/player-manifest.json` содержит checksums всех файлов, исходный revision и digest generated-assets.patch. BuildMac регенерирует canonical scene/imported presentation assets; эти output-only изменения сохранены как provenance patch и не интегрированы в исходники.
Muted native Lab journey прошёл сохранение, ветвление v3 от старой базы, local range errors, discard/delete guards, hidden controls и restart. После restart selection/hash совпали; `history.json` byte-identical. Screenshots просмотрены: `06-history.png` и `11-restarted-revisions.png`, viewport 1440×900. Два Player запуска exit 0. Это automated native evidence, не human/physical acceptance.
Чужие main ProjectSettings сохранены. Временный implementation worktree удаляется после интеграции; pending acceptance app остаётся в постоянном каталоге.

QA history сохранена lossless как `full-final/player-lab/history.json.gz`; duplicate pre-restart payload удалён после byte equality/hash проверки. SHA uncompressed history сохранён в smoke.json.
