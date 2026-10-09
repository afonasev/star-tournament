# Star Tournament

Star Tournament is a native Unity local arena-shooter proving ground. This
checkout is **Unity-only**: [`unity/`](unity/) is the sole executable game.
Browser code was intentionally removed on 2026-09-21; its commits and evidence
remain available as a historical snapshot, not as a runnable fallback.

## Start here

Install/open Unity **6000.3.23f1**, then from the repository root:

```sh
make prepare       # after import or scene-source changes
make test           # EditMode + PlayMode
make check          # canonical tests and macOS Development build
make unity-editor   # open the project in Unity
make unity-run      # open the Player built by `make build` or `make check`
```

`make build` creates `unity/Builds/StarTournamentProvingGround.app`: a local
macOS **Development** Player, not an installer, release or deployable artifact.
Do not run the Editor, batch tests and Player QA against this project at the
same time. `UNITY_EDITOR=/path/to/Unity` overrides the default editor path.
Logs and test XML are written under `.local/unity-evidence/`.

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


## Production 0.1.0 (09.10.2026)

Stable Windows x64/macOS arm64 installers and signed full updates are published through [GitHub Latest](https://github.com/afonasev/star-tournament/releases/latest). Test installations retain their test channel; install the production package to switch channels. User profiles remain outside the payload. OS developer signing/notarization and physical device acceptance are separate from update-package signature verification.

This source includes gamepad match-setup navigation, LT aim, Default v2 packaged balance, nonblocking saved Lab selection and compact update-menu identity. Verification: 406 EditMode / 235 PlayMode tests; 2 menu-state tests; 34 Python / 49 launcher security contracts. Internal source revision: `a7f809332e14878a623b80e85a7181188541a561`. Build preparation regenerates authored scene/prefab data; exact artifact custody is supplied with the release.

Production startup repair: compatibility migration is validated in memory during startup. Existing Lab history stays unchanged until an explicit Lab mutation; old immutable revisions and their hashes are preserved.
