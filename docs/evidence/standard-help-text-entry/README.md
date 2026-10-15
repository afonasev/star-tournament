# Справка и ввод текста по макетам13–15

Требования game-spec-templates@0c2d4562683252d54736f294dc1af463050eac17, каталог0.5.0; code baseline36d19768, ветка codex/game-standard-alignment. Согласовано направление13–15, не Player/human acceptance.

QA: pure production draft EditMode; InputSystem events и реальные InputField/профиль/копия Lab в PlayMode; существующая settings navigation и независимая справка двух viewport. Только shared Editor -noaudio -muted, без Player/full/build/deploy. XML/log.gz и реальные RenderTexture PNG сохранены рядом. Подробный результат/failed attempts в verification.json. Поддельных данных для подтверждения успеха нет: тестовые профили и Lab-история изолированы и удаляются после evidence.

Выноски игровых действий сверены с SeatInputCoordinator. Исправлены Q/E leader, NPOT импорт, пропорции обеих страниц и обрезание эквивалентов в split-view. Автоматизированный reconnect — виртуальное InputSystem device removal/reclaim; физический контроллер этим не проверен. Телевизор, четыре физических устройства, мелкие подписи справки в малых viewport и человеческая визуальная приёмка остаются открытыми. Это focused UI evidence, не FPS benchmark; счётчик на PNG не является замером производительности.
