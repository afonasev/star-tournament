# Визуальная приёмка дневного света

Результат — Well presentation v5 в коммите, содержащем этот файл; source hashes в verification.json. Исходная выбранная концепция2 — codex/well-light-study a55575f1. Итоговая реализация уточняет яркость ламп и корректность HDR grading/render target, сохраняя выбранный солнечный рисунок.

1. Сравнить atrium-before-after.jpg: дневной рисунок/прохладная тень должны подчёркивать этажи и мосты; проходы и окна прежние.
2. Посмотреть after-board.jpg и full-visual/corner-room.png, electronics-room.png, workstation-0/1/2.png: светлая отделка сохраняет швы/фактуру, биология/физика/электроника узнаваемы, нет больших плоских белых пятен от ламп.
3. В full-review-1/2/3.jpg оценить тёмные лифтовые зоны, лестницы, надписи и тематические приборы. Сравнительные кадры — реальные Editor renders штатного кода, без review-only Volume/light overrides.

Можно принять показанный визуальный результат или указать конкретный ракурс/проблему. Физическое прохождение и Player/device/FPS/audio acceptance остаются отдельными. Player build/release/deploy не выполнялись. Тихой автоматической командой для будущей целевой проверки служит tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellDaylightPresentationTests с отдельными testResults/logFile; для человека звук допускается только в пределах заказанного теста.
