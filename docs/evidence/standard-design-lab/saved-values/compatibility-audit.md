# Аудит upgrade-политики Lab, срез Q2

Pin0c2d4562683252d54736f294dc1af463050eac17. Базовый source478417db. Новейшее решение пользователя17.10.2026 сохраняет его настройки баланса.

| Источник/изменение | Поведение после Q2 | Проверка/граница |
| --- | --- | --- |
| Собственный local experiment | Каждое существующее совместимое editable значение переносится точно, включая прежний factory default | History tests,5 compatible legacy generations |
| Авторский project profile/Default | Тот же preservation; выбранный authored Default не заменяется factory snapshot | Три режима в каждом legacy case |
| Новое поле | Из latest package/project release Default; отсутствующее в нём — trusted registry | Existing upgrade-defaults fixture,0/false |
| blood.drops v1 | Собственное10 остаётся10;18 остаётся18 | Обновлён утверждённый own-history contract |
| audio.footstepDistanceMeters v9/v10 | Собственное1.7 остаётся1.7;3.4 остаётся3.4 | Два history cases; serialized scene upgrade отдельный |
| visual eight arena fields v17 | Сохранённые значения не retune; новые ambient/local-light fields получают release Default | История + unchanged standalone scene assertions |
| view.fillIntensity, switch shoulder/elbow | Own значения не retune/clamp; недопустимый угол — отказ | Equipment/medium gesture и retired-range cases |
| well.daylight/fill/lamp v1/v2 | Сохраняются при соответствии текущим constraints; old fill.8 сейчас не проходит step-сетку, явный отказ без подмены | BeforeWellDetail refusal case |
| BotCombatMovementDefaults | Существующие authored values защищены; packaged Default может принять прежнюю approved factory migration | Legacy factory маршрут сохранён |
| rifle/shot torso→damage | Явное структурное отображение сохранено | Не является default retune; отдельный существующий legacy контракт |
| shared gamepad→две оси | Явное структурное отображение сохранено | Отдельный existing legacy контракт |
| lunar v3 office→stair | Явное разделение прежнего общего управления сохранено | Не отменено Q2; полный mapping audit ещё открыт |
| Смена type/range/resource/schema | Trusted проверка; отказ без rounding/clamp, исходные bytes остаются | CompatibilityDiagnosticsTests |
| Immutable исходник | Number/date/hash/snapshot неизменны, current view отдельно | Exact snapshot JSON/hash/date и file bytes/reopen |
| Serialized scene/default | Прежняя factory эволюция сохранена; она не используется для own history | Existing scene assertions |

## Остаток полного3.1

Этот срез не закрывает весь аудит. Требуют отдельного завершения: mixed old/current history migration admission; public source identity у derived local views (technical key/date vs saved number/date); current counterpart/mark/export/startup авторского historical Default; Default/package repeated-upgrade provenance; полная матрица type/removal/resource и per-row UI. Старые строки без provenance не переписываются автоматически: невозможно доказать, что это cache, а не явное человеческое сохранение. Никакие файлы пользователя/канонический author JSON не менялись для проверки.
