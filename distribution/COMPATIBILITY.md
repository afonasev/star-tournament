# Совместимость релизов

Новые пакеты создаются только с подписанными metadata schema2. RSA-PSS/key pin, repository/channel, version/date, full asset identity/hash/size остаются обязательными. Launcher и Unity Player поставляются одним полным `velopack-full-v1` пакетом; отдельного content-only обновления нет.

```json
{
  "schema": 2,
  "compatibility": {
    "minimumOsVersion": "12.0",
    "minimumUpdaterProtocol": 2,
    "payloadFormat": "velopack-full-v1",
    "userDataPolicy": "profiles2-lab3-prefs1"
  }
}
```

Пример — для macOS; Windows floor `10.0.19043`. Версия ОС canonical, без ведущих нулей. macOS producer читает `LSMinimumSystemVersion` фактического Player и отвергает отсутствие/значение ниже Unity6000.3 baseline12.0. Windows floor соответствует Unity6000.3 Player10/21H1. `minimumUpdaterProtocol` означает поколение reader технических metadata, отдельно от Windows maintenance IPC protocol2. Неизвестные payload/data contracts или более высокий reader не допускаются.

Producer пишет этот же compatibility в `release.json` внутри payload. До подписи, при publication collect и после проверки signed package hash перед активацией читается ровно один ограниченный по размеру `release.json` из ZIP. Его application/version/date/platform/architecture/channel и compatibility должны совпасть с signed descriptor. Проверка не распаковывает файлы на диск. Detached signature/hash/config inspection остаётся host-independent; discovery/staged/native activation отдельно требуют совместимый реальный host/capabilities.

Один ограниченный API traversal даёт frozen candidate snapshot, отсортированный по semantic version. Повторяющиеся одинаковые locator entries объединяются; конфликтующие останавливают discovery. Корректно подписанный, но несовместимый target пропускается в пользу следующего совместимого. Неверная подпись/identity/shape, HTTP failure или незавершённая pagination не превращаются в «актуально». Если все targets несовместимы — явная ошибка, без повторного discovery в этой сессии. SDK отдельно исключает downgrade/установленную версию.

## Старые клиенты и protected broker

Подписанные schema1 файлы не переписываются. Новый reader сохраняет исторический fixed contract: Unity baseline ОС, reader1, прежний full payload и legacy data policy. Schema1 с compatibility override отвергается. Новые публикации schema1 запрещены producer/publisher. **Открытый разрыв UPDATES-INSTALL-02:** старый reader, проверяющий `schema==1`, отклоняет schema2. Прежнее решение требовать новый installer не достигает обычного seamless update и не принято как исключение пользователем. Обязателен совместимый подписанный bootstrap и автоматическая миграция protected broker; пока этот путь не реализован, переход существующих клиентов не готов. Исторические опубликованные байты не меняются.

Windows `maintenance.json` новых установщиков содержит `protocol:2, metadataReader:2`. Отсутствие `metadataReader` означает1; legacy playback protocol1 не допускает новое обновление. Effective reader ограничен одновременно исполняемым launcher parser и защищённым broker declaration. При rollback сохраняется новый broker и его metadataReader2, но восстанавливаются playback identity/protocol предыдущего launcher. Поэтому protocol1 остаётся доступным для offline playback. Автоматический переход protocol1→2 ещё не реализован; повторный installer остаётся способом ремонта, не принят обычным шагом обновления. Неизвестная capability не поднимает возможности исполняемого кода.

## Данные и откат

`profiles2-lab3-prefs1` закрепляет `player-profiles-v2.json`, `design-lab-history-v3.json` и совместимые primitive PlayerPrefs. Legacy файлы profiles-v1/Lab-v2 читаются только при отсутствии destination; import не переписывает оригинал, явная mutation пишет новый namespace. Предыдущий Player использует свои файлы. Собственные совместимые значения Lab, включая0/false/old default, сохраняются точно, оригинальные snapshot/hash/date неизменны. Новая генерация несовместимого формата требует нового policy token и согласованных writer/reader/rollback правил; нельзя молча повторно использовать этот token.

Metadata gate подтверждает объявленный и связанный с пакетом контракт, а не физическую приёмку миграции/отката. Actual v0.1.2 namespace/migration fixtures сохранены в `docs/evidence/standard-user-data/`; native installed previous→candidate→failed startup→previous/data acceptance остаётся отдельным gate.

## Границы проверки

Source QA: `docs/evidence/standard-release-compatibility/signed-target/`. Реальные RSA-PSS signatures и C# production sources, ZIP/config binding, simulated OS/reader probes, реальные macOS fixture supervisor processes (без Unity), portable Windows filesystem contracts. Windows ACL/UAC/NSIS runtime, готовая пара launcher/Player, macOS first-install preflight/DMG и физические устройства этим не доказаны. Player/installer/release build, публикация и выкатка требуют отдельной команды человека.

## Переход v1→v2: незавершённая работа

Read-only inventory подтвердил published0.1.5/test.1 schema1 и protocol1 в выпущенном Windows source. Новый API discovery уже поддерживает paired `latest-v2-*` endpoints, предпочитает их при наличии и сохраняет legacy fallback для полностью legacy releases. Modern family без own-architecture primary не подменяется историческим alias; неверная modern signature не вызывает downgrade к legacy metadata. Эти маршруты исполнены на настоящих выпущенных Source classes с namespace-only transform, offline HTTP/RSA; это не готовый bootstrap/installed migration.

Требуемый дальнейший pipeline: подписанный совместимый bootstrap с отдельным broker candidate; subsequent production release хранит immutable old `latest-*` descriptor aliases на этот bootstrap плюс настоящие modern primaries текущей версии. GitHub Latest остаётся текущим выпуском, legacy production reader принимает подписанный older bridge target из alias. В subsequent test releases old names отсутствуют, поэтому старый test selector выбирает последний совместимый bootstrap. Новый reader выбирает modern primaries. Нужны version-bound bridge receipts, producer/collector/publisher/landing wiring и автоматическая protected component transaction. Текущий producer/publisher ещё не подключены к этому переходу — не считать весь pipeline готовым.

Legacy Update.exe backup/fast-callback/startup guarantees должны быть установлены по actual native binary/tool version, а не по NuGet API. Library1.2.158 указывает source3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b; это исходная точка исследования, не доказательство версии shipped native updater. Нельзя приписать старым exe новые snapshot/health guarantees. Bridge/recovery source и native acceptance остаются обязательными.
