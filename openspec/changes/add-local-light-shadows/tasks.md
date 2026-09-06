## 1. Presentation profile и shadow contract

- [x] 1.1 Добавить profile-owned budget, map size и soft radius с metadata/range validation и проверить `presentationProfile.test.ts`.
- [x] 1.2 Реализовать quality-tier resolution 0/1/2/4 и stable camera-relative selection локальных shadow casters; проверить renderer unit tests.

## 2. Renderer integration

- [x] 2.1 Включить soft shadow renderer settings, настроить lamp shadow map и receive/cast flags только для presentation geometry; проверить отсутствие изменения simulation boundary tests.
- [x] 2.2 Сделать shadow resources частью renderer lifecycle и проверить dispose/resize и existing no-idle-submission behavior.

## 3. Проверка

- [x] 3.1 Запустить typecheck, релевантные unit tests и полный `npm test`; исправить все регрессии.
- [x] 3.2 Запустить production browser performance gate для всех quality presets и сохранить результат с effective shadow budget.
- [x] 3.3 Провести muted in-app Browser visual playtest нового dev-стенда, сохранить и проверить PNG с мягкими тенями от ламп.
