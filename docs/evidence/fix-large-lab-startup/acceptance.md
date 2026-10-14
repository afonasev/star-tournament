# Проверка запуска

Открыть сохранённый QA Player по команде агента. Меню должно открыть опубликованный Default без ожидания локальной истории. Пока история не готова, Lab показывает ожидание, запрещает изменение/создание и позволяет вернуться. Матч на Default доступен; окончание проверки не меняет его настройки. После проверки доступны прежние локальные ревизии. Ошибка истории не запрещает Default и не перезаписывает исходный файл.

QA Player будет сохранён отдельно с exact source revision/manifest. Source, full tests, Player, human/device/visual/audio acceptance and deployment are separate gates. Human acceptance pending.
