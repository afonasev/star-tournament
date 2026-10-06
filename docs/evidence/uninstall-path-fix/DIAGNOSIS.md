# Windows uninstall path correction, 06.10.2026

User reported the registered-directory mismatch before deletion. In original un.onInit, StrCpy $INSTDIR "$EXEDIR" overwrote NSIS's initialized original install root with the executing temp-copy directory. Official NSIS manual Chapter4 documents original uninstall INSTDIR and transparently copied executable; Chapter3 documents _?= to suppress copying.

Sources: https://nsis.sourceforge.io/Docs/Chapter4.html#4.2.2 and https://nsis.sourceforge.io/Docs/Chapter3.html#3.2.2. Context7 NSIS lookup returned unrelated NSS/Nsight libraries, so primary NSIS documentation was used.

Fix removes this assignment. Registry path equality, original root, optional missing registration for partial first install, embedded broker ownership/root/idle checks and bounded owned-component deletion remain. Profiles/settings outside release folders and unknown root files remain unaffected.

Verification scope is installer tooling with a real NSIS rebuild over original test.7 payload, 22 Python contracts and every extracted installed-file hash compared after rebuilding. Original Unity checks (342 EditMode + 207 PlayMode, both Release builds) apply to unchanged Player bytes; they were not rerun or claimed as Windows uninstall execution. Physical PC retry remains pending.

New wrapper is published under an immutable installer.2 GitHub release. This does not alter installed version test.7 or signed test.8 update feeds. New wrappers fix subsequent installations; installed root Uninstall.exe is not replaced by an in-game update. For current installations the documented Win+R _?= invocation bypasses temporary copying and can require reboot for removal of the running executable.
