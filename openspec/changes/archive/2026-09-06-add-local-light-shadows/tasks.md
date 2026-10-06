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

## 4. Контрастный light rhythm после visual feedback

- [x] 4.1 Добавить profile-owned global fill и расширенный budget fixtures; проверить descriptor validation и неизменность simulation identity.
- [x] 4.2 Выводить emissive lamp fixtures из wall rhythm corridors, rooms и combat zones; убрать shell shadow casting и проверить стабильность renderer scene.
- [x] 4.3 Провести muted browser visual QA затемнённой арены с видимыми лампами и production performance gate; приложить PNG.

## 5. Чёткие тени без wall flicker

- [x] 5.1 Уточнить presentation contract: чёткие локальные тени видны на полу, wall shell и wall detail не получают их.
- [x] 5.2 Уменьшить profile-owned soft radius и ограничить receive-shadow stable floor geometry; покрыть renderer unit test.
- [x] 5.3 Провести muted browser visual QA и production performance gate, сохранить PNG.

## 6. Устранить z-fighting architectural bays

- [x] 6.1 Зафиксировать positive clearance между GLB wall-bay и canonical wall shell.
- [x] 6.2 Вписать GLB wall-bay внутрь shell с clearance и покрыть renderer unit test.
- [x] 6.3 Провести muted browser visual QA и production performance gate, сохранить PNG.

## 7. Исключить collision-only gap seals из renderer

- [x] 7.1 Зафиксировать collision-only статус overlapping gap seals в renderer contract.
- [x] 7.2 Не создавать WebGL mesh для gap seal и покрыть сцену unit test.
- [x] 7.3 Провести muted browser visual QA и production performance gate, сохранить PNG.

## 8. Устранить пересечения сегментов portal facade

- [x] 8.1 Отделить renderer facade от overlapping collision proxy частей арки.
- [x] 8.2 Сгенерировать разнесённые structural portal GLB segments и проверить asset contract.
- [x] 8.3 Провести muted browser visual QA и production performance gate, сохранить PNG.

## 9. Переназначать локальный свет по близости камеры

- [x] 9.1 Убрать зависимость lit fixtures от порядка обхода арены.
- [x] 9.2 Выбирать ближайшие local lights и их shadow subset вместе; покрыть renderer unit test.
- [x] 9.3 Провести muted browser visual QA и production performance gate, сохранить PNG.

## 10. Устранить depth competition wall-bay с shell

- [x] 10.1 Зафиксировать внешний тонкий фасад `wall-bay` с положительным зазором до opaque wall shell.
- [x] 10.2 Вынести GLB wall-bay перед shell и проверить bounds renderer unit test.
- [x] 10.3 Провести muted browser visual QA и production performance gate, сохранить PNG.
