## MODIFIED Requirements

### Requirement: Анимация non-local робота derived из presentation state
Renderer SHALL производить полный `light-sport-robot-animation-v1` набор из presentation snapshot/event данных: спортивный idle, walk/run/strafe locomotion, take-off/airborne/landing jump, aiming, firing recoil, hit reaction и destruction. Animation controller MUST плавно смешивать текущую и целевую presentation-only позы при смене supported state и формировать непрерывные idle/locomotion циклы без заметного скачка на границе цикла. Он MUST поворачивать только semantic joint groups и presentation-owned weapon mount; он MUST NOT записывать или изменять position, velocity, collision, hit volumes, combat result, snapshot, replay либо state hash. Existing first-person viewmodel, camera и recoil MUST сохранять принятый отдельный contract.

#### Scenario: Locomotion и прыжок
- **WHEN** presentation snapshot описывает non-local участника с направлением движения либо фазой прыжка
- **THEN** renderer показывает согласованную walk/run/strafe или take-off/airborne/landing позу без изменения authoritative transform и collision

#### Scenario: Combat feedback
- **WHEN** presentation получает supported firing или damage event для non-local участника
- **THEN** соответствующий robot показывает краткую aiming/firing recoil или hit reaction через суставы и weapon mount, не создавая дополнительный shot, damage либо ammo change

#### Scenario: Destruction
- **WHEN** non-local participant становится destroyed
- **THEN** renderer проигрывает механическое destruction движение, сохраняет существующий corpse lifecycle и не превращает animation timing в simulation state

#### Scenario: Плавная смена presentation state
- **WHEN** renderer переключает non-local робота между двумя supported animation states
- **THEN** поза плавно приходит к следующему состоянию без одиночного кадра с резким joint displacement и без изменения authoritative state
