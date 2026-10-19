# Startup/elevated broker adapter source verification

12 startup orchestration +59 signed transaction/process-death +48 release compatibility +35 recovery contracts passed (154). Maintenance compile succeeded with0 warnings and0 errors. Earlier initial compile warnings are retained in maintenance-initial.log; actual nullable owner rejection and deferred Windows guards fixed them without suppressions. Existing unrelated test nullable warnings remain in compile/startup logs.

Normal launcher performs migration before install lease/Player, retains original argv in the same normal-user process, and never passes game argv to the elevated helper. Signed retained actor survives a damaged/missing current candidate. Elevated helper rechecks caller identity, fixed launcher image hash, protected root/paths, idle/installer state and lease, then recovers before retrying. Portable fixtures exercise signed actor selection and transaction recovery; Windows ACL/UAC/process APIs were compiled only, NotRun on macOS.

Source slice only: no actual runnable signed candidate, producer/bootstrap mode, old-native handoff, legacy6-arg activation compatibility or payload rollback integration completed. No Unity/game/Player/installer/full test suite/build publication/deploy. UPDATES-INSTALL-02 and tasks2.4–2.6 remain OPEN.
