# Приёмка природного сада «Колодца»

Выбранное направление: концепт 2, уточнение пользователя — дорожки идут от существующих входов из зданий. Код результата и проверенный source digest записаны в verification.json / Git history этого каталога.

Просмотреть реальные PNG `views/atrium-corner.png`, `west-entrance.png`, `east-entrance.png`, `garden-overview.png`, `garden-detail.png`, `landing.png`:

1. Земля сада покрыта низкой природной зеленью; плитняк и сдержанные цветы видны вблизи.
2. Связанная каменная дорожка начинается у западного и восточного проёмов. Она не заканчивается у глухой стены и не создаёт ложных входов с севера/юга.
3. Центральная площадка остаётся открытой, проходы и две прежние клумбы сохранены.
4. Принятое освещение Well v5 и материалы этажей сохраняются.

Автоматические проверки и PNG не являются человеческой приёмкой. Human decision: pending. Player build, физический runtime/device, full suite и release/deploy не заказаны и не выполнены. Сад доступен из исходников; установленный Player не обновлялся.

Тихий QA: `tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellGardenPresentationTests ...`; для Player при отдельной команде использовать `-noaudio -muted` с самого старта.
