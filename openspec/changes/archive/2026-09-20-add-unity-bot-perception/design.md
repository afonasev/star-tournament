## Context

См. proposal.md. NativeCombatSession содержит актуальные позы и жизни, NativeMatchRoster — отношения команд. Planner ещё отсутствует. Human UI и массивы камер привязаны к local seats; их разделение не требуется для проверки восприятия.

## Goals / Non-Goals

**Goals:** создать тестируемую границу между истинным миром и знаниями будущего AI; проверить её на настоящей физике и native Player.

**Non-Goals:** выбор тактики, маршрута, цели стрельбы; подключение AI к setup; полная replay-схема и побитовый replay Unity.

## Decisions

- Scene adapter единственный читает NativeCombatSession. На fixed sample он проверяет живых противников через горизонтальный FOV (как в действующем bot contract) и PhysicsScene ray к torso anchor. Origin использует eyeHeight движения, anchor — существующие hit-zone metadata. Static WorldLayer совпадает с текущим projectile occlusion; movement-only barrier не закрывает LOS. Живые тела не становятся постоянной архитектурной окклюзией восприятия; shot resolver продолжает блокировать дробь союзником.
- Core принимает только допустимые sightings и собственные life/alive, не raw enemy poses. Память хранит последнее наблюдение, его время и life identity. Утрата LOS, скрытое движение/смерть/respawn не обновляют запись. Истечение отсчитывается от исходного observation time. Direct visible флаг сбрасывается на каждом sample.
- Прямые sightings публикуются союзникам с задержкой; сообщения не ретранслируются. Очередь ограничена одной записью receiver/source/target, сохраняет время исходного наблюдения и не откладывает доставку бесконечно при постоянном зрении. Старое сообщение не заменяет более свежее знание; FFA не получает сообщения. Собственная смерть/смена жизни очищает знания и входящую очередь; capture в паузе не вызывается.
- DTO snapshot v1 содержит время, профиль, observers/own-life, память и очередь; чтение и restore копируют данные и валидируют identity/форму. Это локальный контракт продолжения знаний при том же внешнем потоке observations, не реализация игрового replay. Raw world и Unity handles в DTO отсутствуют.
- unity-bot-perception-v1@1 переносит только FOV, memorySeconds и communicationSeconds из botRules. Три уровня и metadata имеют единый registry, доступный существующему Inspector. Никакие combat/movement stats не меняются.
- Вместо полного playable bot выбран внутренний слой, аналогично предыдущему team reducer. Полный вариант одновременно меняет participant/seat mapping, setup, navigation и весь planner; фиктивное ограничение bots до четырёх тел не вводится.

## Risks / Trade-offs

- Скрытая информация через удаление stale записи → hidden death/respawn regression с идентичным потоком знаний.
- Сообщения могут продлевать память или вытеснять свежие данные → исходные timestamps, bounded queue, direct-only publication, tests задержки/expiry/приоритета.
- Snapshot ошибочно примут за полный replay → явная область DTO и отсутствие обещания cross-platform resimulation.
- Internal diagnostic примут за playable AI → маркировка снимков и handoff, неизменный human-only setup.
- Ray к одной torso-точке — проверяемый исходный sensor, не обещание обнаружения любой видимой части тела. Геометрический адаптер заменяем отдельно от памяти.

## Migration Plan

Additive core/adapter и development-only diagnostic на базе 4521a7d; обычный Player не запускает sensor. Проверки, handoff и отдельный коммит; без archive/main/deploy. Откат — revert отдельного коммита.
