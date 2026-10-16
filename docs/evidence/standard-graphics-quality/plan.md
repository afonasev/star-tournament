# План focused applicability QA
Baseline codex/game-standard-alignment@753a3eaa. Изменён только новый EditMode fixture; production graphics/UI/presets не меняются. Unity нужен для AssetDatabase/imported meshes/QualitySettings. Native Editor проверяет effective URP для6 slots и наличие Auto skin с multi-weight vertices. Глобальные quality/AA/VSync/shadows восстанавливаются, исходные ProjectSettings/assets bytes сравниваются после выхода.

Команда: STAR_TOURNAMENT_QUALITY_PROBE=<absolute>/probe.json tools/unity.sh shared -batchmode -nographics -noaudio -muted -runTests -testPlatform EditMode -testFilter StarTournament.ProvingGround.Tests.EditMode.GraphicsQualityApplicabilityTests -testResults <absolute>/editmode.xml -logFile <absolute>/editor.log
Wrapper only Editor/TestRunner, no Player build. Один affected test, без full/playmode/natural matches. No Player/installer/deploy. Headless probe не доказывает visual/FPS/device acceptance.

Context7 ctx7@latest library unity ... завершился fetch failed; использованы official Unity6000.3 SkinnedMeshRenderer.quality/SkinQuality.Auto docs и точный URP17.3.0 package source локального cache. Cached package не менялся.
