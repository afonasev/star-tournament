# Авторские данные Lab

`authoring-history-v1.json` — источник профилей, сохранённых версий и релизных отметок dev-клиента. Файл хранится в Git вне `Assets`, чтобы неотмеченные эксперименты не попадали в Player через Resources. Production использует отдельный каталог поставки и собственные данные игрока.

В Editor откройте `Assets/StarTournament/Scenes/ProvingGround.unity` и включите Play: по умолчанию используется этот проект. Тихий запуск Editor: `UNITY_RUN_MODE=shared unity/tools.sh editor -noaudio -muted`. Development Player из отдельно собранной текущей ревизии принимает `-noaudio -muted -labProject /absolute/path/to/star-tournament/unity`; production этот авторский параметр игнорирует.

Сохранение Lab записывает файл, но не выполняет commit/push, сборку или публикацию. После авторской работы просмотрите diff и сохраните изменения обычным процессом Git. Исходные ID, hash и Integrity не редактируются вручную. Компактная схема содержит исходные снимки и совместимые представления: `Number` может быть внутренним ключом; подписи и проекция опубликованных версий сохраняют первоначальные номера, даты и снимки.

Первичный импорт выполнен явным `LabReleaseBuild.BootstrapAuthoring` из проверенного каталога этой ревизии. Повторный bootstrap отказывается перезаписывать существующий файл. Проверка без записи: `UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -nographics -executeMethod StarTournament.ProvingGround.Editor.LabReleaseBuild.ValidateAuthoring -quit -logFile /absolute/owned/path/authoring-audit.log`.

Проверки и происхождение исходных данных: `docs/evidence/standard-design-lab/runtime/`. Интеграция проекции с инструментом подготовки следующего каталога и оставшиеся требования Lab продолжаются отдельно; изменение отметок само не обновляет Resources или установленную игру.
