# test.10 — публикация принятого меню F4

Команда пользователя: «принято деплой». Опубликован тестовый выпуск **0.1.0-test.10 (08.10.2026)**:
https://github.com/afonasev/star-tournament/releases/tag/v0.1.0-test.10

- Исходники Player: `c6ab890157fdbcf73edf8c1e4efb1c5af01d1a55`; публичное зеркало: `fc297f1b8ed118e2fa3aa8f75b30c3a524e65346`. Runtime/build/asset trees совпадают; последующие изменения achievements в main в этот выпуск не включены.
- Полный старый release gate: 353 EditMode + 216 PlayMode, без ошибок и пропусков; Development build и обе Release-сборки. Этот gate выбран до уточнения пользователем политики QA от 08.10.2026. Новая политика не требует автоматически повторять весь набор для локальных UI-правок.
- 49 C# security contracts и 34 Python distribution contracts прошли.
- macOS Release Player: штатный запуск, клавиатурой главное меню → карта → правила; мышью командный режим → состав. Цель 3500, вкладки без номеров, подсказка выше вкладок, кнопка смены цветов отсутствует. Скриншоты `release-*.jpg`; финальная проверка с `-noaudio`. Это автоматизированная агентом проверка, а не физическая gamepad/Windows-приёмка.
- Development-only `-menuStyleReview` в Release недоступен: первоначальный probe завершён по таймауту, затем заменён обычным CUA UI-smoke; product failure не обнаружен. Подробности `smoke-plan.json`.
- Подписанные манифесты обоих runtime проверены; все 8 публичных файлов скачаны анонимно и побайтово сверены (`publication.json`). Реальный AuthenticatedSource обоих runtime скачал и проверил полный пакет test.10 (`live-*.json`).
- Страница https://robowar.afonasev.tech/ показывает test.10 и обе платформенные ссылки (`landing-readback.txt`).
- При редактировании тела черновика GitHub вернул временный untagged name; до публикации восстановлен точный tag. Финальная публикация через PATCH с явными tag_name/target_commitish/draft=false; затем штатный publisher --resume выполнил remote identity и все public readbacks. Неизменяемые файлы не заменялись.

Пакеты обновления аутентифицированы RSA-PSS/SHA256; сертификаты разработчика ОС/notarization отсутствуют. Физическая установка Windows/macOS и физические устройства остаются отдельными gates. Принятие пользователя относится к F4; публикация test.10 исполнена по отдельной команде, production не публиковался.

Generated Unity inputs сохранены в `generated-build-inputs.patch.gz`, SHA совпадает с identity-файлами. Инвентари обоих Player и asset hashes позволяют проверить точную сборку после удаления промежуточных копий. Публичные установщики/пакеты остаются в неизменяемом GitHub-релизе; локальный принятый F4 Player сохраняется.
