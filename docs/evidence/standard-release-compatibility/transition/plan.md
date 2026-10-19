# Seamless transition audit и routing QA
Baseline codex/game-standard-alignment@46085f83. Обнаружен незакрытый UPDATES-INSTALL-02: current schema2-only pipeline несовместим с реальными schema1/protocol1 clients; ручной installer не принят как обычный путь. Existing published bytes неизменны, только read-only download small signed manifests/identity+Git source inspection.

Первый source этап добавляет optional modern latest-v2-* paired endpoint с прежним Legacy fallback в API discovery. Producer/publisher/broker migration ещё не готовы; endpoint proof не объявлять готовым переходом.

QA:
- `dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --legacy-reader-routing`: actual production/test released source classes, namespace-only transform, schema2 refusal, immutable production legacy descriptor alias under current latest redirect, old test filtering modern-only names, new client semantic selection; offline HTTP+RSA.
- Existing selectors default security, --github-discovery, --release-compatibility, --update-session: общая GetReleaseFeed/discovery изменена, сохранить trust/cache/frozen consent/once и current requirements. Не полный suite.
- `dotnet build --no-restore distribution/maintenance/Maintenance.csproj`: явно linked Source compile, не publish/installer.
- Provenance source copies namespace reversal/source SHA; signed deployed descriptors actual openssl RSA-PSS pinned-key verification; source/tag commit equality inspect (test custody ref differs from tag, source bytes equal).

No Unity/Player/installer/full/deploy. Никаких API writes/releases/tag changes. Windows automatic component migration и legacy pre-start rollback всё ещё обязательны и отдельно tracked. UI Q5–Q10 pending.
