# История версий — макеты04/16 и метка17

**Editor implementation verified.** Пользователь2026-10-16 согласовал16 и попросил выделить текущую версию. Постоянная рамка/полоса/метка17 показаны до реализации и проверены вне фокуса, при готовом новом пакете и resize. C#38 повторно использованы по неизменным source hashes, Unity6/6 latest unique affected. Старые failed attempts сохранены; итоговые PNG в images/current. Player/platform/human acceptance отдельно.


Change align-standard-release-history, code baseline c999afef, ветка codex/game-standard-alignment, game-spec-templates@0c2d4562683252d54736f294dc1af463050eac17, каталог0.5.0.

История отделена от discovery/update consent. Launcher читает публичный GitHub API без токена игрока, выбирает десять опубликованных версий своего канала плюс установленную через точный tag endpoint. Draft исключаются; нет нужного релиза/русского описания — показано отсутствие, без придуманных изменений. Обе языковые части ## Русский / ## English хранятся раздельно; текущее меню игры русское. Cache в пользовательском launcher-data, ограниченные HTTP/cache/IPC, conditional ETag, offline cached marker, Retry-After backoff. Cache не имеет полномочий обновляющего manifest.

«Что нового» доступно и без новой версии. Две колонки04, версии с датами, установленная помечена; независимые scroll списка и описания со scrollbar, фиксированная высота строк и auto reveal фокуса при навигации/resize, правый стик/PgUp/PgDn/мышь, A и обычный Back. Завершение HTTP не переносит фокус. Длинные описания разбиваются на небольшие uGUI Text meshes с сохранением всего текста. Неподдерживаемый markup не исполняется.

History HTTP входит в drain перед match ack; по запросу в матче сеть не стартует, при возвращении в меню продолжается только ранее запрошенная история. Download candidate/version и единственная discovery не меняются.

Методы API и ETag сверены с официальными GitHub REST Releases и Best practices for using REST API; контрактные проверки используют HttpMessageHandler без внешней сети. Автоматические Unity проверки — shared -noaudio -muted, собственные profilesPath/Lab. Ни сборки Player, ни полного набора, ни публикации не было.

Git-derived bilingual publication generator остаётся обязательным следующим этапом UPDATES-CHANGELOG-01. Эта реализация читает языковые разделы, но не переписывает старые GitHub Releases и не утверждает их наличие. Startup-health rollback, Lab05/06 и Windows07 также ещё открыты. Реальный installed launcher/Player, Windows/macOS/TV/physical controls и human visual acceptance отдельно.
