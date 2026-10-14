# Проверка запуска

Открыть сохранённый QA Player по команде агента. Меню должно открыть опубликованный Default без ожидания локальной истории. Пока история не готова, Lab показывает ожидание, запрещает изменение/создание и позволяет вернуться. Матч на Default доступен; окончание проверки не меняет его настройки. После проверки доступны прежние локальные ревизии. Ошибка истории не запрещает Default и не перезаписывает исходный файл.

QA Player: `/Users/eaafonasev/Projects/star-tournament/.local/player-builds/2026-10-14-lab-startup-6adfaa87/StarTournament.app`, исходники ветки `codex/fix-lab-startup`, коммит `6adfaa87` (фактическая интеграция — в planning delivery.json). Manifest и complete.json рядом со сборкой. Source, full tests, Player, human/device/visual/audio acceptance and deployment are separate gates. Human acceptance pending.
