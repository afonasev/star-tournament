import { useEffect, useRef, useState } from 'react'
import * as THREE from 'three'

import { prepareArenaGlbAssets } from '../render/arenaGlbAssets'
import { createRobotGlbPresentation } from '../render/participantWeaponPresentation'
import { type RobotAnimationController, type RobotAnimationState } from '../render/robotAnimation'
import { resolveDeathSlideOffset } from '../render/deathPresentation'
import { PRESENTATION_BALANCED_V1_PROFILE } from '../presentation'

const STATES: readonly RobotAnimationState[] = ['idle', 'walk', 'run', 'strafe-left', 'strafe-right', 'jump-takeoff', 'jump-airborne', 'jump-landing', 'aim', 'fire', 'hit', 'death-hit', 'death-fall', 'death-rest']

export function RobotAnimationReview() {
  const requestedState = new URLSearchParams(window.location.search).get('animationState')
  const initialState: RobotAnimationState = STATES.includes(requestedState as RobotAnimationState)
    ? requestedState as RobotAnimationState
    : 'idle'
  const host = useRef<HTMLDivElement>(null)
  const stateRef = useRef<RobotAnimationState>(initialState)
  const [state, setState] = useState<RobotAnimationState>(initialState)
  const [lod, setLod] = useState<'lod0' | 'lod1'>('lod0')
  const reviewTimer = useRef<number | undefined>(undefined)
  const animation = useRef<RobotAnimationController | undefined>(undefined)
  stateRef.current = state

  useEffect(() => {
    const mount = host.current
    if (mount === null) return
    // This isolated review surface can export its own WebGL frames for visual QA.
    // Gameplay renderers keep their normal transient framebuffer behaviour.
    const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true })
    const scene = new THREE.Scene(); scene.background = new THREE.Color('#07111f')
    const camera = new THREE.PerspectiveCamera(42, 1, .05, 30); camera.position.set(4.2, 1.8, 2.3); camera.lookAt(0, 1.05, .28)
    const key = new THREE.DirectionalLight(0xffffff, 3); key.position.set(3, 5, 4); scene.add(key, new THREE.HemisphereLight(0xb9eaff, 0x102030, 2))
    const floor = new THREE.Mesh(new THREE.CircleGeometry(3.4, 48), new THREE.MeshStandardMaterial({ color: '#0d3144', roughness: .8 })); floor.rotation.x = -Math.PI / 2; scene.add(floor)
    mount.replaceChildren(renderer.domElement)
    let cancelled = false; let frame = 0; let cache: Awaited<ReturnType<typeof prepareArenaGlbAssets>> | undefined
    let presentation: ReturnType<typeof createRobotGlbPresentation> | undefined
    const resize = () => { const { width, height } = mount.getBoundingClientRect(); renderer.setSize(Math.max(1, width), Math.max(1, height), false); camera.aspect = Math.max(1, width) / Math.max(1, height); camera.updateProjectionMatrix() }
    const render = (milliseconds: number) => {
      const elapsed = stateRef.current === 'death-hit' ? 0 : stateRef.current === 'death-fall' ? .275 : Number.POSITIVE_INFINITY
      const fall = resolveDeathSlideOffset(elapsed, Math.PI / 2, PRESENTATION_BALANCED_V1_PROFILE.death.corpseSlideDistanceMeters)
      if (presentation) {
        presentation.group.position.set(fall.x, -fall.progress * .08, fall.z)
        presentation.group.rotation.set(0, Math.PI / 2, fall.progress * Math.PI / 2, 'YXZ')
        presentation.animation?.present(stateRef.current, milliseconds / 1000)
      }
      renderer.render(scene, camera); frame = requestAnimationFrame(render)
    }
    resize(); const observer = new ResizeObserver(resize); observer.observe(mount)
    void prepareArenaGlbAssets().then((loaded) => {
      if (cancelled) { loaded.dispose(); return }
      cache = loaded
      presentation = createRobotGlbPresentation({ geometries: [], materials: [] }, cache, new THREE.Texture(), '#42d9ff', lod)
      animation.current = presentation.animation
      animation.current?.present(stateRef.current, performance.now() / 1000)
      scene.add(presentation.group)
      frame = requestAnimationFrame(render)
    }).catch(() => { mount.dataset.animationReviewError = 'asset-load-failed' })
    return () => { cancelled = true; animation.current = undefined; cancelAnimationFrame(frame); observer.disconnect(); cache?.dispose(); renderer.dispose() }
  }, [lod])

  useEffect(() => () => { if (reviewTimer.current !== undefined) window.clearTimeout(reviewTimer.current) }, [])
  const selectState = (next: RobotAnimationState) => {
    stateRef.current = next
    setState(next)
    // Background in-app Browser tabs can throttle RAF; the review controls must
    // still show the selected pose before the tester captures a frame.
    animation.current?.present(next, performance.now() / 1000)
  }
  const runFullReview = (index = 0) => {
    selectState(STATES[index]!)
    if (index + 1 >= STATES.length) return
    // Leave the target pose on screen long enough for its visible blend to land.
    reviewTimer.current = window.setTimeout(() => runFullReview(index + 1), 1250)
  }
  const runDeathReview = (index = 0) => {
    const phases: readonly RobotAnimationState[] = ['death-hit', 'death-fall', 'death-rest']
    selectState(phases[index]!)
    if (index + 1 >= phases.length) return
    reviewTimer.current = window.setTimeout(() => runDeathReview(index + 1), 650)
  }

  return <main className="animation-review" data-muted="true">
    <section className="animation-review__panel"><p>RENDERER-ONLY · MUTED</p><h1>Robot animation review</h1><span>Не матч, не replay и не проверка physical mouse-look.</span>
      <div className="animation-review__controls"><button type="button" onClick={() => runFullReview()}>full review</button><button type="button" onClick={() => runDeathReview()}>death review</button>{STATES.map((candidate) => <button key={candidate} type="button" data-active={state === candidate} onClick={() => selectState(candidate)}>{candidate}</button>)}</div>
      <label>LOD <select value={lod} onChange={(event) => setLod(event.target.value as 'lod0' | 'lod1')}><option value="lod0">LOD0</option><option value="lod1">LOD1</option></select></label>
    </section>
    <div ref={host} className="animation-review__viewport" aria-label="Просмотр анимации робота" />
  </main>
}
