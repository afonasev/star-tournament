import { createRoot, type Root } from 'react-dom/client'

import { GameplayHud } from './GameplayHud'
import type { GameplayUiSnapshot } from './gameplayUiSnapshot'

export interface GameplayUi {
  readonly update: (snapshot: Readonly<GameplayUiSnapshot>) => void
  readonly dispose: () => void
}

export interface GameplayUiOptions {
  readonly onRequestControl?: () => void
  readonly onRepeat?: () => void
  readonly onMainMenu?: () => void
  readonly onToggleFullscreen?: () => void
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
          <GameplayHud snapshot={snapshot} onRequestControl={options.onRequestControl} onRepeat={options.onRepeat} onMainMenu={options.onMainMenu} onToggleFullscreen={options.onToggleFullscreen} />,
        )
      }
    },
    dispose() {
      if (disposed) return
      disposed = true
      // The shell callback can replace the parent React tree from inside a HUD
      // event. Crossing a microtask boundary avoids synchronously unmounting
      // this nested root while React is still processing that parent update.
      queueMicrotask(() => root.unmount())
    },
  }
}
