# Проверка baseline OS guards

12 production-policy проверок passed (injected Windows/macOS boundary probes плюс actual host API).49 security,34 feedback,10 macOS recovery scenarios passed; последние используют реальные fixture supervisor processes, не Unity Player.20 Python NSIS/route tests passed; makensis -PPO только разворачивает macros, installer не создаётся. Строки `Built ...` в Python log принадлежат mocked compiler route. Maintenance script compilation:0 errors/0 warnings. Tests build сохраняет прежние nullable warnings startup-health fixtures.

Manual source path audit: launcher guard после чтения config до lock/source/discovery/Player; Windows activate/elevated apply guard в начале; installer plan/begin/commit guard до leases/mutations. macOS prepare guard до capture, apply guard до staging, recover/watch отдельно. NSIS init guard до broker extraction; un.onInit не получает guard. Эти source/probe checks не являются реальной Windows/devices acceptance.

Current fixed floor основан на official Unity6000.3 Desktop Player requirements и macOSTargetOSVersion12.0. Старый published v0.1.2 использовал тот же6000.3.23f1. Schema1 не изменялась; новые signed target constraints/updater/data policy ещё OPEN. Рабочие установки, опубликованные manifests и user data не изменены.
