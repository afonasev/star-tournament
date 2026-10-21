# UI gate exact NUnit case contract

Baseline: `00fbd33209eb755dd5f86499aa8ea12867a2efd0` (`main`). Scope is limited to `tools/check_ui.py` and `tools/test_check_ui.py`; Unity runtime, fixture bodies, and `SUITES` selection remain unchanged.

Source review found 3 `LocalSeatLayoutTests`, 5 `FourPlayerMenuTests`, and 15 `IndependentPauseMenuTests` NUnit cases. All currently use one case per method; there are no parameterized cases in these selected classes. The gate now encodes their exact fully qualified NUnit names with explicit multiplicities. A passing run must contain exactly those selected cases, each passing; missing, duplicate, skipped, and foreign/unexpected cases fail.

The successful saved XML at `docs/evidence/add-industrial-tunnels-map/full-check/{editmode,playmode}.xml` predates several current selected test methods. Positive parser/comparison tests validate the exact selected case names present in those saved runs. They are not represented as proof that the current full selected set has run.

Verification: `python3 -m unittest test_check_ui` from `tools/` — 9/9 passed; `python3 -m py_compile check_ui.py test_check_ui.py` — passed; `python3 tools/check_ui.py --plan` — passed with the unchanged UI-only selection; `git diff --check` — passed. These checks exercised the full current fixture-name map, duplicate/missing/unexpected/skipped/failed/incomplete inputs, and positive saved XML case sets.

Unity execution, full game suite, Player build, merge, release, and deployment were not run or authorized. The saved positive XML is historical and does not contain all 23 currently selected cases; current complete-set acceptance is verified using the enumerated source-backed names in the tooling tests, not by claiming those historical runs are current.
