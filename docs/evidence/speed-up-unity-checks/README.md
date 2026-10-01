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
