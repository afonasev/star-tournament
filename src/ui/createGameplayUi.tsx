import { createRoot, type Root } from 'react-dom/client'

import { GameplayHud } from './GameplayHud'
import type { GameplayUiSnapshot } from './gameplayUiSnapshot'

export interface GameplayUi {
  readonly update: (snapshot: Readonly<GameplayUiSnapshot>) => void
  readonly dispose: () => void
}

export interface GameplayUiOptions {
  readonly onRequestControl?: () => void
}

export function createGameplayUi(
  mount: HTMLElement,
  options: GameplayUiOptions = {},
): GameplayUi {
  const root: Root = createRoot(mount)
  let disposed = false

  return {
    update(snapshot) {
      if (!disposed) {
        root.render(
          <GameplayHud snapshot={snapshot} onRequestControl={options.onRequestControl} />,
        )
      }
    },
    dispose() {
      if (disposed) return
      disposed = true
      root.unmount()
    },
  }
}
