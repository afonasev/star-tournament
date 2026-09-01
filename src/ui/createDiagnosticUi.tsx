import { createRoot, type Root } from 'react-dom/client'

import type { UiSnapshot } from '../runtime/uiSnapshot'
import { DiagnosticOverlay } from './DiagnosticOverlay'

export interface DiagnosticUi {
  readonly update: (snapshot: Readonly<UiSnapshot>) => void
  readonly dispose: () => void
}

export function createDiagnosticUi(mount: HTMLElement): DiagnosticUi {
  const root: Root = createRoot(mount)
  let disposed = false

  return {
    update(snapshot) {
      if (!disposed) root.render(<DiagnosticOverlay snapshot={snapshot} />)
    },
    dispose() {
      if (disposed) return
      disposed = true
      root.unmount()
    },
  }
}
