# Unity FPS — 2026-09-19

Mac Development Player из `codex/unity-fps-settings`, muted. Profile `unity-proving-ground-v1` version 1.
EditMode 8/8; PlayMode 9/9; build PASS. XML/logs в `.local/unity-evidence/`, hashes в `summary.json`.

Native UI проверен мышью: setup on/off, закрытие и повторный запуск с FPS off, diagnostic live on/off, pause on/off и resume. Видны все четыре viewport; overlay справа сверху не перекрывает HUD/прицел. Diagnostic участники неподвижны, устройства не назначены. FPS на screenshot — моментальное display evidence, не performance acceptance; physical TV/four-device и reference-hardware gates остаются открытыми.

- `setup-on.png`, `setup-off.png`: настройка и результат.
- `restart-off.png`: сохранение после перезапуска.
- `live-on.png`, `live-off.png`: четыре viewport.
- `pause-on.png`, `pause-off.png`: счётчик в паузе.

API сверены по Unity 6.3: [unscaledDeltaTime](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-unscaledDeltaTime.html), [PlayerPrefs.Save](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerPrefs.Save.html). Context7 lookup вернул fetch failed.
