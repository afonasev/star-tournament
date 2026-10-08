# SN30 Pro match entry

Baseline: 5d77b3e. Isolated scene prepared through unity/tools.sh prepare, without Player build.

Before fix, SN30 submit followed by keyboard navigation release in the same input update assigned Keyboard. After fix, zero navigation and released mouse buttons do not claim menu ownership. DefaultRosterTests passed 6/6, including the exact SN30 HID layout, a second connected pad, keyboard/mouse release races and intentional keyboard/mouse entry. XML and source hashes are retained here. This proves synthetic Editor input, not physical installed Player acceptance.

Player build, installed update, physical-controller acceptance and release publication are deferred under the user policy of 2026-10-08. The installed test.10 remains unchanged.

The initial test in the primary checkout failed during Lab catalog initialization because its generated scene was stale. Preparing the isolated scene resolved that failure. Generated scene/prefab/project-setting changes from preparation are QA derivatives and are not included in the patch; the prepared scene hash is in tested-source.json.

Additional checks passed: make check-ui (3 EditMode + 12 PlayMode), EightBitDoSn30ProInputTests (2 EditMode), source diff whitespace check and strict OpenSpec validation.
