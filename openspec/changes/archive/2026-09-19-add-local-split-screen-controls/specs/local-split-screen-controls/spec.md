## Purpose

Capability определяет локальную игру 1–4 людей на одном экране: явные привязки устройств, нормализованные игровые действия, безопасное восстановление контроллеров и честные viewport.

## ADDED Requirements

### Requirement: Уникальные локальные seats и устройства
Browser setup SHALL позволять создать от одного до четырёх local seats в составе до восьми participants. Каждый seat MUST иметь ровно одну привязку: единственную keyboard/mouse пару либо один конкретный подключённый gamepad; одно физическое устройство MUST NOT принадлежать двум seats. Недоступная или конфликтующая привязка MUST блокировать Start с понятной причиной.

#### Scenario: Смешанный состав
- **WHEN** setup назначает keyboard/mouse P1 и два разных подключённых геймпада P2 и P3
- **THEN** configuration принимает три local seats и сохраняет каждую привязку независимо от participant color и team

#### Scenario: Конфликт устройства
- **WHEN** пользователь назначает один gamepad двум seats либо keyboard/mouse второму seat
- **THEN** Start блокируется, а UI указывает обе конфликтующие карточки

### Requirement: Standard gamepad actions
Назначенный gamepad SHALL нормализовать левый stick в movement, правый stick в look, `RT` в fire, `A` в jump, удерживание `View` в scoreboard и `Menu/Start` в pause. `LB` MUST не создавать действие. Физические browser events MUST NOT входить в сериализуемый action frame.

#### Scenario: Удерживание View
- **WHEN** игрок удерживает и отпускает `View` во время running, overtime либо killcam
- **THEN** scoreboard видим только во время удержания без изменения gameplay state hash

### Requirement: Disconnect и восстановление
Disconnect назначенного gamepad, потеря document focus либо потеря keyboard/mouse pointer lock SHALL очистить held actions всех seats и поставить общий матч на паузу. Pause UI MUST назвать затронутый seat и позволить продолжить только после восстановления исходного устройства либо явного переназначения в setup.

#### Scenario: Контроллер отключён
- **WHEN** назначенный P2 gamepad становится недоступен во время running
- **THEN** ни один gameplay tick не выполняется после pause, а P1/P3 не получают input P2

### Requirement: Равные viewport и постоянный счёт для трёх игроков
Один seat SHALL получать полный экран; два seats — равные левый и правый viewport; четыре seats — равную сетку 2×2. Три seats SHALL получать три равных игровые ячейки сетки 2×2, а оставшаяся ячейка SHALL постоянно показывать live-счёт и MUST NOT принимать игровой ввод.

#### Scenario: Матч трёх игроков
- **WHEN** запускается configuration с тремя local seats
- **THEN** каждый игрок получает одинаковый размер игрового viewport, а четвёртая ячейка показывает актуальную simulation projection
