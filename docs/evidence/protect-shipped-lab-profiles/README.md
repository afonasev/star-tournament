# Shipped Lab profiles: copy before editing

Source: `8fc435654fdf525e3ce9f1b06e97dcfb8456217c` (runtime implementation `9de7820c80f5f054659db482f6b4d7d38a1ffc94`; subsequent commit only corrects a test assertion).

Selected gate: **UI**. Permission controls and workspace UI change; balance values, simulation, serializers and shipped catalogue contents do not change.

- UI gate: 15 passed (`ui-editmode.xml`, `ui-playmode.xml`, `ui-result.json`).
- History/catalogue: 54 passed (`history.xml`), including immutable source bytes, whole-profile lock on old local revisions, copy identity and preservation through catalogue updates.
- Workspace: 1 passed (`workspace.xml`), including disabled original controls, editable copy and existing draft/immutable match checks.
- Native development Player build: succeeded.

All 70 selected tests passed. This is not a new full gameplay gate or human/device/audio acceptance. The catalogue used by these tests remains trusted input; local release marks alone do not lock a local profile. No user history migration/deletion occurs.

Native Player screenshots and completion records are produced with isolated QA history and muted audio. Candidate identity and complete file manifest checksum bind the retained local app to the tested source. External publication and human acceptance remain separate.
