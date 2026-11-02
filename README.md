# Star Tournament

<!-- desktop-downloads:start -->
**Скачать / Download**

| Windows | macOS |
| --- | --- |
| **[Download EXE](https://github.com/afonasev/star-tournament/releases/download/v0.1.12/Star-Tournament-0.1.12-Windows-x64-Setup.exe)** | **[Download DMG](https://github.com/afonasev/star-tournament/releases/download/v0.1.12/Star-Tournament-0.1.12-macOS-arm64.dmg)** |

[Только установщики / Installer releases](https://github.com/afonasev/star-tournament/releases?q=tag%3Av0%20prerelease%3Afalse)

Installer version: **0.1.12**.
<!-- desktop-downloads:end -->

Star Tournament is a native Unity local arena-shooter proving ground. This
checkout is **Unity-only**: [`unity/`](unity/) is the sole executable game.
Browser code was intentionally removed on 2026-09-21; its commits and evidence
remain available as a historical snapshot, not as a runnable fallback.

## Start here

Install/open Unity **6000.3.23f1**, then from the repository root:

```sh
make prepare       # after import or scene-source changes
make check-tooling  # QA routing/XML contract tests, no Unity
make check          # local UI contracts, no Player build
make unity-editor   # open the project in Unity
make unity-run      # open the Player explicitly built by `make build`
```

`make build` creates `unity/Builds/StarTournamentProvingGround.app`: a local
macOS **Development** Player, not an installer, release or deployable artifact.
Do not run the Editor, batch tests and Player QA against this project at the
same time. `UNITY_EDITOR=/path/to/Unity` overrides the default editor path.
Logs and test XML are written under `.local/unity-evidence/`.

Choose the smallest sufficient fixtures from the recorded baseline/diff and dependencies.
Full tests (`make test`, `check-full`, `test-edit`, `test-play`) require separate
human confirmation, including production. Only then set `CONFIRM_FULL_TESTS=1`;
an agent must not grant itself authorization. Build/deploy permission does not
include full tests. `check-player` builds only; affected QA is selected separately.
Test wrappers isolate Lab history per run; explicit `STAR_TOURNAMENT_QA_LAB_HISTORY`
is preserved for history-specific QA. Focused examples and policy:
[QA scope](.agents/references/qa-scope.md).

Run `make help` for all supported commands. The trooper asset pipelines are
deliberately confirmation-gated (`make trooper-help` first); they author local
derivatives/evidence and never deploy or distribute anything.

## Repository map

- [`unity/`](unity/) — Unity project; its README lists scene, controls and
  native Player QA entrypoints.
- [`docs/GAME_SPEC.md`](docs/GAME_SPEC.md) — approved product and architecture
  overview.
- [`docs/OPEN_SPEC_PLANNING.md`](docs/OPEN_SPEC_PLANNING.md) — canonical
  planning home and lifecycle instructions.
- [`docs/HISTORICAL_SNAPSHOT.md`](docs/HISTORICAL_SNAPSHOT.md) — exactly what
  browser-era OpenSpec/docs/evidence/artifacts/balance history remain, and why
  they are not runtime.
- [`scripts/trooper/README.md`](scripts/trooper/README.md) — offline asset
  authoring prerequisites and limits.

There is no `make deploy` target. Native distribution, release identity,
target environment and smoke procedure require a separately approved decision.


## Download and distribution

Download test Windows/macOS installers from https://robowar.afonasev.tech or the GitHub Releases of https://github.com/afonasev/star-tournament. Windows installation uses Program Files with UAC; both optional Desktop shortcut and launch actions are on the final screen. Updates require explicit user consent and verify an RSA-PSS/SHA256 signed descriptor plus package size/hash. Private signing keys and local build/generated data are excluded from Git. See `distribution/README.md` and `THIRD_PARTY_NOTICES.md`.

Тихий запуск Player на всю сессию без изменения сохранённых настроек: `UNITY_RUN_MODE=shared unity/tools.sh run -noaudio -muted` (для выбранной сборки добавьте `UNITY_PLAYER_APP=/absolute/path/StarTournament.app`). Тихий Editor: `UNITY_RUN_MODE=shared unity/tools.sh editor -noaudio -muted`. Автоматические batch QA/build маршруты передают `-noaudio` с самого старта.
