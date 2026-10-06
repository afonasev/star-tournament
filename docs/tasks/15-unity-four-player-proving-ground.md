# 15 — Unity: четырёхместный проверочный полигон

Статус: **реализация и автоматические проверки готовы; physical/performance acceptance открыта**. Change: `add-unity-four-player-proving-ground`, ветка `codex/unity-stage0`. Browser backlog закрыт отдельно в `07150e0`; здесь нет обязательства его дописывать. Интеграция и архивирование этого change не выполнены.

## Что можно проверить

Самостоятельный Unity 6000.3.23f1 / URP Player содержит четыре управляемых local seats (четыре gamepads либо keyboard/mouse + три gamepads), отдельные камеры и HUD, setup/pause/rebind, остановку при focus loss/disconnect и явный resume. Одна фиксированная двухэтажная арена содержит рампу, настоящие ступени, низкий потолок, окно и разные movement/shot queries. CharacterController владеет движением; native NavMesh использует параметры этого же motor и выдаёт путь без второго владельца Transform.

Robot, world shotgun и first-person shotgun импортированы из browser baseline через manifest с hash/units/pivot. Inspector профиля использует общий registry с range validation. Диагностический выстрел показывает результат ray query; полноценного боя, здоровья/смерти, анимационного цикла, ботов, генератора, runtime Balance Lab, replay и сети здесь нет. Сглаживание ступеней и сравнение ощущения управления требуют отдельной физической проверки.

## Воспроизведение

Из корня worktree:

```sh
unity/tools.sh test-edit
unity/tools.sh test-play
unity/tools.sh build
open unity/Builds/StarTournamentProvingGround.app
```

Не запускать Editor/tests одновременно с Player benchmark. [README](../../unity/README.md) описывает управление и CLI. Mac Development Player находится в `unity/Builds/StarTournamentProvingGround.app`; это локальный артефакт, не installer/release. Windows support module пока не установлен.

Для одной диагностической ячейки (абсолютный evidence path выбирает вызывающий):

```sh
open -n unity/Builds/StarTournamentProvingGround.app --args \
  -diagnostic -probeCameras 4 -probeEvidence /absolute/evidence/fhd-4 \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

Повторить для 1/2/4 камер и 1920×1080 / 3840×2160. Все варианты оставляют четыре неподвижных капсулы; это сравнение стоимости камер, не разных матчей. Player прогревается 3 секунды, затем пишет 10 секунд и screenshot. Пересекающий warmup кадр исключается целиком. Проверять фактический output и state в report, а не только аргументы CLI.

## Доказательства и границы

[Матрица и три скриншота](../evidence/unity-four-player-2026-09-19/README.md), [результаты и точные hashes](../evidence/unity-four-player-2026-09-19/summary.json); полные локальные XML/логи — `.local/unity-evidence/`. EditMode: 6/6; PlayMode: 8/8. Проверены registry/сериализация, конкретная keyboard/mouse пара, уникальность устройств, очистка/одноразовое потребление input, gamepad-only join/start/pause/resume, отключение, стены/потолок/контакт капсул, физический проход stairs/ramp в обе стороны и следование motor по рассчитанному NavMesh пути. Эти тесты используют виртуальные устройства и не заменяют четыре физических контроллера.

Native screenshot проверяет импорт моделей, materials/shaders, четыре viewport и overlay. Диагностика выполняется на Mac14,10 / M2 Pro / 32 GB / macOS 14.6.1, Development build, URP renderScale 1, MSAA 1. Focus не сохранялся весь интервал; диагностический helper разрешает background execution и явно отражает это в JSON. Это ограничивает сопоставимость и не позволяет объявить 60 FPS acceptance, даже если p95 укладывается в 16.67 ms. Редкие длинные кадры не отфильтрованы. CPU/GPU доступны только в тех отчётах, где соответствующий availability flag=true. FrameTimingManager возвращает асинхронные последние samples: их CPU worst (до 468 ms) расходится с измеренным unscaled frame worst (до 26 ms). Samples пока не связаны по timestamp с измерительным окном; CPU/GPU перцентили сохраняются как сырая диагностика, а не точный performance gate. Для приёмки нужна привязка timestamps или Profiler capture.

Предыдущие прогоны с background stall и кадром на границе warmup исключены из итоговой матрицы; исходные файлы оставлены локально в каталогах `*-invalid`. Каталоги `*-pre-expanded-timing` относятся к предыдущему формату телеметрии и также не входят в итог. Сравнимый browser/Unity benchmark не проводился.

## Что осталось для приёмки

- Утвердить минимальное reference hardware, TV/output, quality/internal scale и критерий редких длинных кадров; сейчас утверждены только 4 игрока, Full HD–4K и минимум 60 FPS.
- Пройти четырьмя реальными устройствами меню, независимое движение/взгляд/прыжок/выстрел, общий pause, focus/cursor loss, disconnect/reconnect и rebind. Отдельно проверить gamepad-only и keyboard + 3 gamepads.
- Провести длительный foreground Player playtest и измерения на согласованном железе, включая одновременное движение четырёх игроков. Текущие неподвижные 10 секунд не представляют будущую боевую нагрузку.
- По конкретным motor/navigation дефектам решать необходимость ECM2/A* сравнения. Платные пакеты не куплены и не испытаны; бесплатный baseline проверен, превосходство над альтернативами не заявляется.
- После обязательной приёмки последовательно интегрировать, синхронизировать OpenSpec specs и архивировать отдельным коммитом. Не публиковать Unity через browser deploy.
