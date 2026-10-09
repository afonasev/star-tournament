# Production 0.1.2 — installer icon only

User authorization: «выкати еще фикс иконки». Packaging source `38c146665e23b5110b761c7d7c45ec85aaad113e`. Public source records exact NSIS/test blob changes, with the preceding public tree preserved.

Game, launcher and maintenance source is unchanged from fully verified 0.1.1. Both previous signed full packages were authenticated against retained SHA256 identities before extracting 350 game files. Repackaged game entries must match every prior SHA256. The prior 413 EditMode/240 PlayMode evidence with retained failures and passing fixture retries, ordinary macOS MAIN_MENU_READY and unchanged original Lab history apply to these exact bytes; no fresh Unity build/test/native/device acceptance is claimed.

Current scoped checks: 34 distribution Python tests (including real NSIS preprocessing, no skips) and 49 launcher security contracts passed. The original 0.1.1 installer fails actual PE icon comparison, reproducing the bug. New real installer must match all seven approved ICO entries and resource image bytes.

Local macOS NSIS 3.13 full compression invocation aborted with `std::bad_alloc`; original log retained. Packaging rerun in a fresh output directory with existing pinned gfe NSIS 3.10, without modifying the installer contract or icon asset. macOS arm64 package complete. Windows compile/readbacks/SDK and final publication evidence follow.

Physical Windows Explorer and wizard appearance, OS installation/update and physical controllers remain separate acceptance. Existing published release files are immutable.

Windows real NSIS compile completed. Full installer SHA256 was checked against the remote output by build_installer.py; actual PE group 14/103/1033 matches all seven approved icon metadata/image resources. Both repackaged full packages preserve all 350 previous game file SHA256s. See icon-after.json and reused-payload-verification.json.
