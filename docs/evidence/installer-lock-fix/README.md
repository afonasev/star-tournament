# Windows installer W2 repair — 04.10.2026

User reported «Не удалось создать блокировку установки» in the Windows test.5 wizard. NSIS `FindFirst`/`FindNext` leave the persistent error flag set on normal end of enumeration; `ReadRegStr` also sets it when a new/partial installation has no registry entry. The first lock check inherited that state despite a successful file operation.

Source fix `6425142` resets the flag before creating the installation directory, writing the recovery marker and uninstaller, and creating the uninstall lock. Directory creation now has its own immediate failure check. Existing protection, elevation, broker validation, and update consent remain unchanged.

Verification: 16 focused NSIS contracts and 20 Python distribution contracts passed, including the new regression and installed compiler preprocessing. [Official NSIS documentation](https://nsis.sourceforge.io/Docs/Chapter4.html#4.9.5.13) defines the persistent error behavior. The full original Unity release gate remains bound to unchanged game bytes; this repair changes the installer wrapper only.

Installer revision 2 keeps installed version test.5 and its test.6 update experiment. It has a new immutable URL, so cached test.5 Setup bytes cannot replace it. The original SHA256-verified Setup archive is the exact source of the unchanged payload; the replacement archive must extract to identical payload paths/hashes. No signing key is needed for this wrapper repair; signed update metadata stays unchanged.

User retry: close the failed wizard, download the new installer from the root landing, install to the default folder, and check first launch. Then test the explicit test.5→test.6 update. Windows wizard/UAC/gameplay acceptance stays open until the user reports the result; successful compilation is not Windows execution evidence.
