## 1. Риг и asset contract

- [x] 1.1 Перестроить оба authored robot GLB в одинаковую rigid semantic joint hierarchy с weapon mount, сохранив neutral pose и принятые material/LOD contracts; проверить source auditом отсутствие skins/clips, mandatory joints и rest bounds.
- [x] 1.2 Добавить regression tests generator/GLB, подтверждающие hierarchy, LOD parity, identity features и неизменность accepted first-person/weapon assets.

## 2. Renderer-only controller

- [x] 2.1 Реализовать presentation-owned robot animation controller для idle, walk, run, strafe, jump phases, aim, fire, hit и destruction; проверить unit tests, что pose изменяет только named joints и возвращается к neutral pose.
- [x] 2.2 Подключить controller к non-local renderer projection и weapon mount, сохранив existing first-person camera/recoil и authoritative transform; проверить renderer tests на event/motion mapping, color/respawn и отсутствие mutation snapshot.

## 3. Animation review

- [x] 3.1 Добавить отдельный muted animation-review sandbox с third-person камерой, state controls, LOD switch и последовательным полным прогоном; проверить browser-facing DOM/runtime test без gameplay input или simulation clock.

## 4. Проверка

- [x] 4.1 Выполнить typecheck, focused unit/source tests, production build, strict OpenSpec validation и diff check; зафиксировать результаты в handoff task.
- [x] 4.2 Выполнить muted in-app Browser review sandbox и игровой renderer, сохранить и визуально проверить PNG для idle, locomotion, jump, aim/fire, hit, destruction и LOD1; подтвердить HTTP URL dev-стенда и явно отделить это от physical mouse-look проверки.
