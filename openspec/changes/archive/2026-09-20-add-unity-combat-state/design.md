## Context

См. proposal.md. ProvingGround владеет motor/input/cameras, боевого состояния ещё нет. Используем существующий NumericDescriptor/ProvingProfile и JsonUtility; никаких новых пакетов.

## Goals / Non-Goals

**Goals:** один владелец lifecycle, независимый snapshot, профиль с metadata, предсказуемые переходы по simulation time.
**Non-Goals:** подключение к FixedUpdate/Physics/UI, разрешение geometry damage, teams/scoring, запись replay и restore session.

## Decisions

- Новый CombatLife — обычный C# объект, CombatLifeState — serializable DTO. Core принимает resolved damage от будущего combat resolver. Это позволяет проверить смерти/боезапас отдельно от physics; альтернативное хранение в CharacterMotor смешало бы movement и бой.
- Life identity — монотонный номер внутри participant ID; kill attribution хранит обе identity. Read возвращает копию. Export JSON является DTO evidence, не утверждённым replay/restore protocol.
- Advance получает только gameplay delta; пауза реализуется отсутствием ticks. ClearHeldInput снимает перенесённое нажатие для будущего pause/focus adapter; устройство не знает о здоровье.
- Отдельная factory `ProvingProfile.CreateCombatDefault` использует тот же descriptor registry; существующий Inspector уже строится по этим metadata. Никакой новый числовой gameplay хардкод вне factory. Начальные ammo/cooldown/killcam из browser prototype-v1; здоровье 100 соответствует текущему headshot contract. Настройки данного профиля ещё не применяются к полигону.
- Respawn — две части: core отмечает readiness, будущий adapter обязан немедленно выбрать лучший свободный spawn и вызвать Respawn. Это не альтернативное игровое правило ожидания.
- Shot request — текущее held значение, core распознаёт front-edge и удерживает его через cooldown. Adapter не должен передавать уже edge-only импульсы без release.

## Risks / Trade-offs

- Core без адаптера не даёт playable combat → handoff прямо называет это domain-only частью этапа 2.
- JSON DTO не является restore API → не обещать совместимость replay.
- Unity scene lifecycle из старых additive tests оставляет объекты → решить при следующем runtime match/session adapter, не включать посторонний fix сюда.
- Нет физических устройств и hardware acceptance → сохранить Stage 0 gates.

## Migration Plan

Новый код не подключается к scene. Удаление новых классов/factory откатывает срез без изменения native UI. Интеграция/архив отдельно после review и завершения условий; deploy отсутствует.
