# OPT-GRAPHICS-QUALITY: подтверждённая применимость
Baseline codex/game-standard-alignment@753a3eaa; target settings0.1.4/template60fe3d2. Это applicability evidence, не implementation/visual/performance acceptance.

## Native Editor probe
Один EditMode test прошёл (1 passed/0 failed/0 skipped, XML duration0.0589778s). Реально загружены TrooperBody/Arms prefab meshes;17 SkinnedMeshRenderer используют Auto, суммарно9585 multi-weight vertices. Engine QualitySettings skinWeights отличается по слотам. Original quality/AA/VSync/shadows восстановлены; QualitySettings/GraphicsSettings/URP/prefab bytes сверены с baseline и неизменны. Editor автоматически добавил пробел ShaderGraph.m_Name — точное собственное изменение удалено.

| Slot | Skin weights limit | URP shadowDistance | URP MSAA |
|---|---:|---:|---:|
| Very Low | 1 | 50.0 | 1 |
| Low | 2 | 50.0 | 1 |
| Medium | 2 | 50.0 | 1 |
| High | 2 | 50.0 | 1 |
| Very High | 4 | 50.0 | 1 |
| Ultra | 255 | 50.0 | 1 |

Все6 engine slots эффективно разрешаются в один ProvingURP.asset; renderScale1, shadowDistance50, MSAA1. Built-in полей shadow/AA в QualitySettings недостаточно, чтобы обещать6 различных URP уровней. NativeVisualPresentation отдельно задаёт SMAA per-camera, графические preferences отдельно владеют VSync/тенями/эффектами. Runtime не вызывает SetQualityLevel; selector отсутствует.

## Вывод и граница
OPT-GRAPHICS-QUALITY нельзя объявить N/A: есть реальные Auto skin consumers с несколькими костями на вершину и разные engine limits. При этом Low/Medium/High имеют одинаковый лимит2; полезность различий mipmap/aniso/LOD/прочих полей для текущих assets ещё требует отдельного разбора. Не выдавать имена6 slots за6 доказанных пресетов. Новые presets/числа не введены, production UI и пользовательские preferences/Lab баланс не менялись. Следующий scope — полная семантика уровней и конкретный макет до UI реализации.

## Источники
- Unity6000.3 [SkinnedMeshRenderer.quality](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SkinnedMeshRenderer-quality.html), [SkinQuality.Auto](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SkinQuality.Auto.html): Auto связан с global skin weights; mesh data не переписывается этим выбором.
- [URP asset reference6000.3](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/urp-reference/universalrp-asset).
- Точный локальный URP17.3.0 source, UniversalRenderPipeline.cs:257–264 синхронизирует QualitySettings AA с asset.msaaSampleCount; shadow atlas параметры берутся из URP asset. Cached package не менялся, package-lock/version сохранён.
- Context7 library lookup завершился fetch failed, использованы official docs/source, не сторонние обзоры.

Первый Editor запуск — NotRun из-за неподдерживаемого в project NUnit атрибута NonParallelizable. Удалён только несовместимый атрибут; единственный тест и все assertions/state restoration сохранены. Финальный Editor exit0, -noaudio -muted с самого старта, shared capacity освобождена. Нет Player/full/build/deploy и нет визуального или FPS вывода из headless probe.
