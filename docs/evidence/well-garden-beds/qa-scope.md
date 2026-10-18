# Affected QA: uniform planting

Baseline: main 4e697a85881b27f89f034f382328f1c723ca7f67. Change: replace the two procedural 3D leaf meshes with the exact same MeadowTuft helper, cutout texture, grass material and submesh as the lawn. No map, profile, lighting, path, collision or gameplay source changes.

Minimum: real Unity PlayMode `WellGardenPresentationTests`, with additional assertions for both beds, soil footprint, low height relative to bed soil, same material/submesh, absent old leaf mesh; retains all existing byte-exact authoring, entrance path, landing, light/furniture, mesh budget and GPU RGB checks. Inspect all six PNGs. No unrelated EditMode migration/full tests, Player build or deploy.

Command from owned worktree:
`STAR_TOURNAMENT_WELL_GARDEN_OUTPUT=<repo>/docs/evidence/well-garden-beds/views tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellGardenPresentationTests -testResults <repo>/docs/evidence/well-garden-beds/playmode.xml -logFile <repo>/docs/evidence/well-garden-beds/playmode.log`
