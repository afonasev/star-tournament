# Логические типы параметров Lab

Unit=boolean явно ограничен0/1: metadata min0/max1/step1/default0or1 и значение0or1. Нечисловые/дробные значения не округляются. Действительные0/1 и история сохраняются. Actual declarations — blood.enabled и hit.enabled; потребители используют логическое включение, не силу эффекта.

Уточнение аудита: общий LabBundle.Validate уже блокировал0.5 по шагу1. ProvingProfile.Validate не имел отдельной boolean-проверки; этот срез добавляет явный типовой контракт и понятную причину, а не исправляет якобы существовавший обход обычного Lab Save.

19 unique affected EditMode passed:17 typed/metadata/false-true/history contracts и2 доверенных storage-boundary cases. Первые2 spoof cases failed из-за неверного предположения теста о caller step; после подмены Unit и Step trusted registry отвергает запись, bytes/hash сохраняются.1 PlayMode passed на текущем numeric UI:0,5 остаётся видимым, запись заблокирована, активный выбор и файл прежние; исправленное0 сохраняется и перечитывается для обоих ключей.

Макеты18–19 предложены и показаны, сохранены в ветке codex/game-standard-alignment,06fd5de9; planning Q1 открыт. Новые редакторы/список исторических версий пока не реализованы. Изменение metadata/value validation не меняет defaults/баланс. Full/Player/build/deploy не запускались, visual/device/human acceptance не заявляется.
