# Сверка сохранённых source milestones с полной матрицей

Baseline `codex/game-standard-alignment@4aa5ff40`. Этот срез меняет только документацию/evidence и planning, не runtime или реальные Lab-файлы. Нормативные110 requirement IDs и30 option IDs не менялись; scope не уменьшен. Все source20–31 уже согласованы, повторное согласование не требуется.

## Проверено сейчас

- Fetch общего template repo: local9cc4415, origin/main60fe3d2; единственное отличие local — AGENTS.md. Все10 ранее закреплённых нормативных SHA256 совпадают, все110 заголовков требований и30 опций точно соответствуют JSON, без missing/extra.
- Существующие Q5/Q6/Q7/Q8/Q9/Q10 коммиты являются ancestors текущего source HEAD. Актуальные области исходников инспектированы; current source fingerprints и exact milestone refs сохранены в verification.json.
- Повторно прочитаны9 сохранённых XML: успешные результаты без skipped, в том числе Q5 initial8/final4 =9 unique, Q6 pause4, Q7 author fallback4, Q10 Edit6/Play4. Это reuse результатов конкретных source milestones и их позднейшего affected QA, не новый запуск на сегодняшнем HEAD и не сумма в общее число тестов.
- Основная таблица больше не называет Q5/Q7/Q10 не реализованными.28 строк детальной Lab matrix синхронизированы со структурированными assessments;30 опций теперь имеют полные option_assessments, а не только перечисление IDs. Причины N/A сохранены; quality остаётся применимой, native/hardware proof открыт.
- is_completion_certificate=false. Source/Editor/recording proof не подменяет installed Windows/macOS, physical/TV/human acceptance. Матрица остаётся110/30, хотя часть gate ещё отсутствует.

## Legacy Windows: подтверждение незакрытого разрыва

Повторно прочитаны released-source d9c985a0 maintenance Activate/Apply и pinned native apply_windows_impl. Старый6-arg Activate хранит restartArgs только в normal supervisor; elevated native apply получает --norestart без этих args. Pinned native сначала извлекает новый пакет, затем вызывает ОБСОЛЕТНЫЙ hook старого locator, затем force-stop root, и лишь после заменяет current. Этот source path не даёт новому payload доказанного pre-stop authority/argv handoff. Native root-kill policy и отсутствие old Player ready-handshake уже описаны в native-updater/legacy-handoff evidence; текущий повтор их не исправляет и не доказывает фактическое поведение чужой установки.

URL pinned source: https://github.com/velopack/velopack/blob/3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b/src/bins/src/commands/apply_windows_impl.rs
SHA256 повторно прочитанного файла: d3db6cfa465053f24245d41ab9532a002091829b2d051eb9942bc64147450d4d

Compatibility Q1 остаётся решённым: автоматический переход обязателен, выпуск ждёт проверки; manual installer exception отсутствует. Q2 спрашивает только доступный Windows ПК/host/VM для изолированной проверки. Q2 блокирует verified/native этап, не реализацию оставшейся source работы. Ответ на него ещё не получен, отсутствие ответа не считается согласием или отсутствием машины.

## Продолжение

Агент готовит конкретный изолированный Windows reproduction/handoff сценарий по действительным old bytes и authority boundary, сохраняет original args/recovery требования, не использует optional diagnostic logs как гарантированный контракт и не подписывает ложный bootstrap. Человек указывает Windows среду через Q2. Параллельно остаются canonical spec synchronization/integration и финальные requirement-by-requirement native gates; до их доказательства полная цель ACTIVE. Actual DMG/Player/installer build и publication требуют отдельной явной команды. Никаких full/Unity/Player/installer/build/deploy в этом срезе.

Owned временный source download удаляется после фиксации URL/hash; исходники кода, устойчивые доказательства и current authoring JSON сохранены в Git. Чужие main/planning изменения не затронуты.
