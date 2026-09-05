## 1. Плавная presentation-анимация

- [x] 1.1 Обновить GAME_SPEC и renderer-only controller: строить target pose отдельно от отображённой и сглаживать semantic joint rotations time-based easing; проверить unit test перехода без teleport и отсутствие mutation model root/simulation inputs.
- [x] 1.2 Настроить непрерывные idle/locomotion циклы и короткие реакции fire/hit; проверить test, что результат согласован при разных шагах времени и возвращается к neutral target.

## 2. Review и проверка

- [x] 2.1 Обновить animation-review full run для наблюдаемого blend и browser-facing test/DOM; проверить, что viewer остаётся muted и renderer-only.
- [x] 2.2 Выполнить typecheck, focused tests, production build, strict OpenSpec validation и diff check; сохранить и визуально проверить PNG/GIF плавного full review из muted in-app Browser.
