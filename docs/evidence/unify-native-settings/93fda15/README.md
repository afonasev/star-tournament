# Приёмка единых настроек

Результат: `93fda15096fdef54f2e1fdd714b9cffcb9684e53`. macOS Player: `/Users/eaafonasev/Projects/star-tournament/.local/releases/unify-native-settings/93fda15/StarTournamentProvingGround.app`. SHA-256 executable: `99f5a1d3b4f26e5ae2f610baf2dc0c883dcd97360966b6b78e35be4d769e7611`. Manifest: `/Users/eaafonasev/Projects/star-tournament/.local/releases/unify-native-settings/93fda15/sha256-files.json`. Только локальный кандидат, внешняя публикация не выполнялась.

19 автоматических проверок прошли, сборка успешна. Muted native Player с 1 и 4 viewport завершился успешно; actual размер 3456×2160. Это synthetic/native visual evidence, не приёмка человеком и не физическая проверка контроллера.

## Проверка человеком

1. Главное меню → Настройки: Изображение, Управление, Интерфейс, Звук. В Управлении открыть справку клавиатуры/мыши и геймпада, прокрутить вниз, затем устройства и вернуться.
2. Проверить те же разделы и параметры в настройках профиля, личной паузе одного игрока и 4 viewport, а также операторской паузе all-AI.
3. Личные чувствительности, выравнивание и FPS относятся к выбранному профилю/гостю; изменения соседнего игрока их не меняют. Тени и громкость общие.
4. На физическом геймпаде проверить D-pad/стик, A, B: параметр → раздел → выход; вложенная справка/устройства/разрешение сначала возвращаются к вызвавшей кнопке. Каждый геймпад управляет своим viewport.
5. Из паузы изменить режим экрана: подтвердить или отменить. Таймер, потеря focus и закрытие владельца откатывают неподтверждённое изменение; соседний viewport не заменяет transaction.
6. Закрытие одного личного меню оставляет общую паузу до закрытия последнего.

## Evidence

Полный набор screenshots/state JSON/logs: `/Users/eaafonasev/Projects/star-tournament/docs/evidence/unify-native-settings/93fda15/one` и `four`. В той же папке verification.json, release.json, XML проверок и build.log.

Запуск через общую обёртку (не запуская параллельно чужой Player):

```sh
UNITY_PLAYER_APP='/Users/eaafonasev/Projects/star-tournament/.local/releases/unify-native-settings/93fda15/StarTournamentProvingGround.app' /Users/eaafonasev/Projects/star-tournament/unity/tools.sh run
```

Human acceptance: pending. Явное решение должно относиться к этому Player и указанному scope.
