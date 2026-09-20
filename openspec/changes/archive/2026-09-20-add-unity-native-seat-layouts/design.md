## Context

См. proposal.md. Read-only astra_architect подтвердил: camera-only reduction оставляет четырёх участников, а presenter индексирует session по длине камер. NativeMatchState запрещает один seat. Solo policy ждёт ответа пользователя; зависимая часть не реализуется до решения.

## Goals / Non-Goals

**Goals:** точные N participants=N human seats в этом срезе; единая геометрия UI/cameras; неизменённый scoring и безопасный Repeat.
**Non-Goals:** независимая bot/network participant модель, восемь участников, смена arena/physics/оружия, performance acceptance.

## Decisions

- ActiveSeatCount отдельно от capacity четыре. Setup меняет число, освобождает только удалённые assignments и очищает очереди. Ready/join/capture/HasKeyboard работают по активным slots. Старт замораживает число; Repeat его сохраняет.
- Сохраняется принадлежащий сцене пул четырёх motor/camera/body/view объектов. Перед новым матчем Stop/Dispose, выключить все capsules, включить ровно N motor roots, передать session/presenter массивы ровно N. Это сохраняет один AudioListener/EventSystem/arena/UI и исключает churn GLB. Полный scene reload и camera-only reduction отвергнуты. Временно seat i=participant i; будущие боты потребуют отдельного binding contract.
- Shared LocalSeatLayout задаёт структурные viewport fractions. Canvas использует viewport roots; HUD и локальная таблица находятся внутри своего root. Оставшаяся четверть при N=3 — постоянная таблица без input/camera. Таблицы скрывают неиспользованные строки; обновление читает snapshot один раз.
- UI использует существующие ui.fontSize/headingFontSize. Лимит мест, единичный/половинный viewport и сетка — утверждённая структура, не баланс. Новые tuning constants не вводятся.
- Действия keyboard/gamepad сохраняются. Keyboard требует mouse capture; focus/cursor loss и active disconnect очищают ввод и приостанавливают весь tick. Disconnect status называет seat. Reconnect допускает только явный Resume, rebind только setup. Общий menu остаётся управляемым штатным UI module.
- Старый -probeCameras остаётся отдельной нагрузочной диагностикой: четыре stationary participants и 1/2/4 камеры, не доказательство playable состава. Новый review проверяет реальный N roster через adapter.

## Risks / Trade-offs

- Dispose снова включает bodies/views → выключение неактивных owners после Dispose, exact-N presenter arrays.
- Старые строки таблиц после уменьшения состава → каждый Show скрывает/очищает лишние rows.
- Трёхместная ячейка не рендерит камеру → непрозрачный UI фон полностью покрывает её; raycastTarget=false.
- Синтетические устройства не подтверждают физическую играбельность → native PNG/state отдельно от открытых physical/TV/performance gates.

## Migration Plan

Изолированная ветка поверх 001824b. Независимый 2–4 контур: spec→code→EditMode/PlayMode→build→muted FHD/4K Player→handoff20→commit. Main/archive/deploy не выполняются. Rollback — предыдущий commit.
