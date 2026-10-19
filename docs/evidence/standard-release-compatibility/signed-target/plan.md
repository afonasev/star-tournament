# План affected QA
Baseline: codex/game-standard-alignment@c54304ca. Signed schema2 OS/reader/payload/data descriptor + actual package config binding; schema1 historical fixed contract. Single discovery snapshot with signed compatible-candidate fallback. Windows maintenance IPC remains2, explicit metadataReader capability and rollback retention separate.

Команды:
- .NET restore from local NuGet, затем Launcher.Contracts.csproj --release-compatibility: настоящий signature/parser/admission/config-in-zip + frozen discovery + capability tests.
- Existing selectors --github-discovery, --update-session и default security: изменён общий AuthenticatedSource, сохранить trust/cancellation/cache и legacy detached fixture contracts.
- --feedback: новый compatibility error reason и old download quarantine.
- --mac-recovery, --windows-installer-transaction, --windows-installer-transfer: config record/capability restore и candidate validation затронуты; процессы fixtures не Unity, silent.
- Python test_release_compatibility.py: реальный openssl PSS + publisher verifier, producer functions, zip binding. test_github_publisher.py и test_windows_stable_entry.py — adjacent publishing/stable-entry routes mocked, без сети и build.
- dotnet build --no-restore maintenance: compilation, не publish/installer.
- Python AST изменённых producer/publisher modules, git diff --check.

Unity runtime/migration code не менялся, предыдущая actual user-data/own-balance QA остаётся в силе, не заменяется текстовой consistency проверкой policy token. Native installed pair/data rollback, Player build/full/installer build/deploy не запускать без отдельной команды.
