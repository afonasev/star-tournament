> Статус: CLOSED / CANCELLED — 2026-09-19. По решению пользователя оставшаяся browser-реализация и приёмка сняты из-за перехода на Unity. Исторические checkbox сохранены (6/9); 3.1–3.3 не пройдены. Delta specs не синхронизируются в основные specs.

## 1. Конфигурация и детерминированный ввод

- [x] 1.1 Расширить versioned match configuration до 1–4 local seats и unique device-binding descriptors, сохранив compatibility path для historical single-seat payload; проверить unit-тестами valid/invalid rosters, duplicate bindings и content hash.
- [x] 1.2 Реализовать per-seat gamepad adapter и общий input coordinator с standard action map (`LS`/`RS`, `RT`, `A`, held `View`, `Menu/Start`); проверить normalised action frames, edge/held semantics и отсутствие `LB` action.
- [x] 1.3 Расширить pause/focus/pointer-lock lifecycle на все local adapters; проверить очистку held input и отсутствие новых simulation ticks при blur, lock loss и gamepad disconnect.

## 2. Локальные seats и presentation

- [x] 2.1 Реализовать setup roster 1–4 людей, выбор конкретного доступного устройства, fast join и понятную validation UI для conflict/unavailable binding; проверить React component tests и сохранение выбранной configuration.
- [x] 2.2 Расширить renderer до stable per-seat cameras и viewport/scissor layouts: 1 full, 2 left/right, 3 equal 2×2 cameras плюс persistent standings cell, 4 equal 2×2; проверить renderer/layout tests без изменения simulation state.
- [x] 2.3 Привязать DOM HUD, held scoreboard и pause/reconnect surface к local viewport; для трёх seats отрисовать постоянный live-score в свободной ячейке; проверить UI projection и accessibility labels.

## 3. Интеграция и приёмка

- [ ] 3.1 Добавить integration/replay tests для общего tick нескольких local seats, participant collision, camera ownership и compatibility migration; выполнить `npm test` и `npm run check`.
- [ ] 3.2 Запустить muted dev-стенд из изолированного worktree, проверить его HTTP-доступность и провести in-app Browser playtest для layouts 1/2/3/4, keyboard/mouse pointer lock, `View`/`Tab`, pause и reconnect; сохранить актуальные скриншоты каждого изменённого состояния.
- [ ] 3.3 Провести физическую проверку реальными назначенными gamepad для смешанного состава и disconnect/reconnect; выполнить split-screen performance gate для 1/2/3/4 seats и приложить результаты к handoff.
