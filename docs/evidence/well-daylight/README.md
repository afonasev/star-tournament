# «Колодец»: выбранный дневной свет

18.10.2026 пользователь выбрал направление 2 по реальным кадрам a55575f1 и отдельно дал команду «делай» (чат 01a14bb9-0977-7ff3-b5e9-c8ba37a10732). Это согласование направления/реализации, не приёмка итогового runtime.

## Результат

Well presentation v5: новый азимут и высота солнца, более выраженный дневной рисунок, прохладная тень; спокойные потолочные лампы и три локальных теневых источника из прежних 24. Финальный raw lamp7 и Lighting scale2 дают10.5 при default50; fill .32 с общим visual lift даёт cool ambient около(.306,.342,.402). Key2 / pitch48 / yaw-55 / shadows.85. Neutral tonemapping и добавочная экспозиция +.35EV при ColorGrading50 сохраняют назначение существующих graphics controls. Настройки имеют Lab metadata.

HDR grading LUT создаётся в transient копии URP asset только для Well v5. Gamma, renderer data, исходный URP asset и остальные карты сохраняются; Apply не создаёт новые копии, off/zero выключает map-local tone/exposure, teardown/смена карты восстанавливает прежний pipeline override. Сброс общего directional fixture исключает утечку Well tint/azimuth в другую карту. Старые v4 и более ранние записи сохраняют доверенные registries/hashes; custom values переносятся, untouched defaults обновляются один раз.

Authored map @4, окна/пути/коллизии, материалы этажей, безопасные надписи, 56 корпусов мебели и число ламп24 не менялись. Тени только у СВ потолочной лампы каждого этажа: три штатные карты512 помещаются в существующий atlas1024. Предупреждений об автоматическом уменьшении atlas resolution в финальном render log нет. FPS не измерялся.

## Проверка

Точные команды/выбор scope: qa-scope.md; XML и сжатые логи рядом. Финальная матрица проверок и source hashes: verification.json. PlayMode проверяет off/zero, независимый Bloom, повторный Apply, возврат исходных intensities, HDR override lifetime, Shadows off/on и переход Well→Combat Bowl. Exact map JSON,24 lamps,56 furniture и отсутствие cosmetic colliders проверяются исполняемыми fixtures. Миграции проверяют старый/новый формат файлов, default/custom tuning, точные исходные hashes и повторное открытие.

47 финальных raw PNG: before/ и after/ (14 одинаковых ракурсов с историческим v4 и штатным v5), full-visual/ (33 HDR detail views). JPG boards только собирают/масштабируют исходные PNG, без цветовых/экспозиционных поправок. Финальные before/after boards и все три full-review pages осмотрены. В угловых комнатах световое пятно больше не стирает швы панелей; цвета этажей и проходы различимы. Это оценка агента, не человеческая приёмка.

## Сохранённая диагностика

compile-notrun/: два старта без исполненных тестов (read-only URP setter и неоднозначный enum); исправлены. migration-initial/:10 Passed/2 Failed из-за недопустимого custom fill в новом тесте (.35 вместо допустимого .37); валидный ввод исправлен, exact hash/custom/restart assertions сохранены. Последующие XML отдельно. initial-runtime/, ldr-refinement/, ldr-target/, hdr-hotspot/ сохраняют причины визуального уточнения: слишком сильные лампы, LDR LUT clamp и LDR target старого detail fixture. Последний теперь использует DefaultHDR как штатная камера. Итог калиброван после просмотра полного набора, а не только обзорного ракурса. Superseded image retention описан в intermediate-retention.json; XML/логи и характерные исходные свидетели сохранены.

## Границы и продолжение

Все автономные запуски через shared wrapper с -noaudio -muted с самого старта. Full tests, Player build, installer, публикация и deploy не запрашивались и не выполнялись. Эти результаты не являются Player/device/audio/performance acceptance. Следующий шаг — человеческая визуальная приёмка точного результата по after-board.jpg, atrium-before-after.jpg и full-visual/. Инструкция: acceptance.md. Сохранённый concept2 не заменяет эту приёмку. Модель «build только по необходимости/команде, deploy отдельно» имеет приоритет над старым lifecycle helper, поэтому без выдуманного deploy stage сохраняется merged с открытой приёмкой; обычный owned cleanup выполняется отдельно.
