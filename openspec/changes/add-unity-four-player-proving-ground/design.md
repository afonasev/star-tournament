## Context

См. proposal.md. Пользователь подтвердил четыре управляемых места, native physics и replay по состояниям. Эталон browser — `8844e40`, release `prototype-v1@12`, schema 10, hash `fnv1a64-v1:5edb6318d1ffaa45`, generator `arena-rooms-v2`. Старый backlog не является обязательством дописывать browser. На машине есть Unity 6000.3.23f1, URP 17.3.0, Mac support; M2 Pro/32 GB/macOS 14.6.1. Архитектурный read-only review выполнен; его прежнее предложение двух seats заменено явным решением четырёх.

## Goals / Non-Goals

**Goals:** один самостоятельный Player-полигон, где можно проверить четыре независимых устройства, motor и маршруты на одной геометрии. Измерения отделены от acceptance.

**Non-Goals:** перенос TypeScript класса в C#, реализация всего roadmap, replay/restore, Windows delivery, платные пакеты. Тестовый shot probe подтверждает aim/occlusion, не объявляет полный combat готовым.

## Decisions

- Unity-проект живёт в `unity/`; generated Library/Temp/Logs/UserSettings/Builds не коммитятся. Editor build entrypoint создаёт стартовую сцену и URP settings воспроизводимо; Unity .meta входят в Git.
- `Runtime/Core` содержит serializable participant state/action/profile DTO, не renderer handles. `Runtime/Motor` — единственный владелец gameplay movement через CharacterController. Runner выполняет motor один раз на fixed tick и публикует state. Камеры и модели читают его. AI Navigation выдаёт путь; NavMeshAgent не двигает Transform параллельно motor.
- `Runtime/Input` связывает LocalSeat с уникальным InputDevice, исключая глобальный Gamepad.current. Первый запуск имеет четыре slots; Space присоединяет keyboard/mouse, Start — свободный gamepad. Запуск возможен после назначения четырёх slots; unavailable devices показываются явно. Поддержаны четыре gamepads либо одна keyboard/mouse пара плюс три gamepads. Отдельный явно подписанный diagnostic camera режим допустим без устройств, но не считается physical playtest.
- WASD/мышь, Space jump, primary button fire, Tab held roster, Escape pause; gamepad LS/RS, south jump, RT fire, View held roster, Start pause, LB свободен. Потеря focus или назначенного устройства очищает команды и ставит общий pause. Resume явный, только с доступными прежними устройствами; rebind возвращает в setup. Native cursor lock включается после Start/Resume, освобождается в pause.
- Cameras: 2×2 для четырёх, full для диагностического одного, left/right для двух, три + standings в четвёртой ячейке. Single-seat workload является benchmark-only; продуктовый первый срез стартует четырьмя. Per-camera uGUI, никакого DOM. Единственный audio listener, QA muted.
- `unity-proving-ground-v1` хранит применяемые числа в одном registry с path/group/label/description/unit/min/max/step. UI редактора профиля и validation используют эти же metadata; editor tooling достаточно для этого среза, runtime Lab позже. Берём движение из browser release как начальную настройку, а не гарантируем прежнюю траекторию.
- Одна authored геометрическая fixture является источником colliders и NavMesh bake: два закрытых уровня, две разнесённые связи (ramp/stairs), низкий потолок, окно и projectile-transparent barrier. Статические модели читают ту же геометрию. Тесты обязаны проверять отсутствие ложных XZ-связей, обе стороны transitions, headroom и контакты живых капсул.
- CharacterController выбран бесплатным baseline. ECM2 сравнивается при подтверждённых версии/лицензии, если baseline требует значительной собственной обработки. Opsive шире нужного motor и потребует аудита camera/animation ownership. AI Navigation baseline; A* только при измеренном ограничении. Dungeon Architect Snap Grid Flow — кандидат этапа 3, не зависимость полигона.
- glTFast editor import существующих GLB в prefab; semantic mounts, метры, ориентация, materials/normal/ORM и Player shader availability проверяются отдельно. Отсутствующий asset показывает диагностическую ошибку, а primitives не выдаются за завершённую asset acceptance.
- Performance: Full HD/4K output, фиксировать internal scale и quality, 1/2/4 cameras; p50/p95/p99/worst frame, CPU/GPU при доступности, memory, warmup/measurement durations. Цель четырёх игроков — 60 FPS; без утверждённого железа/quality итог имеет статус diagnostic, не PASS финальной производительности.

## Risks / Trade-offs

- Native motor не гарантирует command replay → отдельный state-recorded replay позже, никакой обещанной cross-platform parity.
- Одновременно движущиеся капсулы и stairs могут выявить ограничения CharacterController → записать failing fixture, не наращивать собственную физику без сравнения ECM2.
- Нет четырёх реальных gamepads/TV → automation и Player build сохраняются, physical gate остаётся открытым.
- Registry может предложить пакет новее документации → pin exact versions + lock, compile/test в установленном Editor обязательны.
- Runtime bake нельзя считать доказательством физической проходимости → motor traverse tests и native ручная проверка отдельно.

## Migration Plan

Полигон не заменяет browser release и не деплоится автоматически. Сохраняем baseline identity и первичные evidence; Unity выбирается основной версией только по позднему delivery change. Удаление/отключение `unity/` полностью изолирует эксперимент от browser.

## Open Questions

Reference PC/TV, финальная platform matrix, quality/internal scale и frame-time методика; подробный replay/restore формат; лицензии альтернатив. Они не блокируют бесплатный локальный полигон, но блокируют соответствующую финальную приёмку/покупки.
