## Why

Нужно проверить, дают ли штатные Unity-системы подходящее движение, многоэтажную навигацию и игру четырёх людей на одном экране до переноса полного матча. Пользователь утвердил Unity-native физику, replay по состояниям и первый срез сразу с четырьмя игроками.

## What Changes

- Отдельный Unity 6000.3.23f1 / URP 17.3.0 проект в `unity/`, native Mac Player и воспроизводимая сборка.
- Фиксированная двухэтажная арена-полигон: рампа, настоящие ступени, перекрывающиеся этажи, низкий потолок, разные movement/projectile препятствия.
- Четыре управляемых local seats, Input System, отдельные cameras/uGUI, 2×2 layout, явные device bindings, общий pause/focus/reconnect.
- CharacterController как бесплатный исходный motor; AI Navigation как route provider без второго владельца движения. Сравнение готовых платных решений пока документальное; покупок нет.
- Существующие robot/shotgun GLB, диагностический shot probe, профиль с descriptor metadata и native test/build/evidence workflow.
- Browser baseline `8844e40` сохраняется. Старые TS реализации и проверки являются справочными; новый runtime не обещает совместимость hashes/seeds/replay.

Не входят полный матч/scoring/death/respawn, боевые боты, procedural generator, полноценный Balance Lab, replay writer/player, Windows delivery, сеть и покупки. Цель 60 FPS четырём игрокам при Full HD–4K утверждена, но reference hardware/TV, quality tier/internal scale и метод длинных кадров открыты. Benchmark на M2 Pro предварительный; отсутствие физических контроллеров оставляет gate открытым.

## Capabilities

### New Capabilities
- `unity-four-player-proving-ground`: независимый native полигон для четырёх local seats с измеряемым motor/navigation/input/asset contract.

### Modified Capabilities
Нет: browser runtime не переписывается и его завершённость не объявляется.

## Impact

`docs/GAME_SPEC.md` §§2–3, 6–8, 10–11; Unity roadmap и native QA references. Новые файлы только в `unity/` и evidence/docs. Зависимости: URP, Input System, AI Navigation, glTFast, Unity Test Framework; точные resolved versions фиксируются в package lock после импорта. Состояние gameplay отделено от устройств, UI, камер и render objects. Схема replay, historical compatibility и сетевая модель не выбираются этим change.
