# Loading investigation — partial cache fix

Reported Player main 0d3676b6 waited while loading the real 153 MiB Lab history. The copied history validates successfully in Editor in 108.37 seconds, unchanged SHA-256. The complete UI remains blocked until validation is done; eliminating that wait needs the startup-order choice posed to the user.

Cache lookup now precedes historical schema transforms, and exact linear hits are retained. Selected validation, migration, cancellation and read-only protection checks pass. The failed new-test setup (no persisted history file) is retained with the corrected passing rerun. No full tests or new Player build. Original app/history remain unchanged.

Retained reproduction input, user log, silent Player log and process sample: `/Users/eaafonasev/Projects/star-tournament/.local/loading-hang-2026-10-14/`. No reproduction Player is left running.
