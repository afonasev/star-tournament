import { readFileSync, readdirSync } from 'node:fs'
import { dirname, extname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

const IMPORT_MODULE_PATTERN =
  /(?:\b(?:import|export)\s+(?:type\s+)?(?:[^'";]*?\s+from\s+)?|\b(?:import|require)\s*\(\s*)['"]([^'"]+)['"]/gu
const VALUE_IMPORT_PATTERN =
  /^\s*import\s+(?!type\b)([\s\S]*?)\s+from\s+['"]([^'"]+)['"]/gmu
const DYNAMIC_OR_REQUIRE_IMPORT_PATTERN =
  /\b(?:import|require)\s*\(\s*['"]([^'"]+)['"]\s*\)/gu

const SIMULATION_FORBIDDEN_GLOBALS = [
  /\bwindow\b/u,
  /\bdocument\b/u,
  /\bnavigator\b/u,
  /\b(?:Keyboard|Mouse|Pointer|Touch|Wheel|Gamepad)Event\b/u,
  /\b(?:HTMLElement|HTMLCanvasElement|Document|Window|ResizeObserver|WebGLRenderingContext)\b/u,
  /\b(?:requestAnimationFrame|cancelAnimationFrame)\b/u,
  /\b(?:localStorage|sessionStorage)\b/u,
]

const SIMULATION_FORBIDDEN_WALL_CLOCK = [
  /\bperformance(?:\.now)?\b/u,
  /\bDate\.now\s*\(/u,
  /\bnew\s+Date\s*\(/u,
  /\b(?:setTimeout|setInterval)\s*\(/u,
]

const SIMULATION_FORBIDDEN_BACKEND_TYPES = [
  /\b(?:Collider|RigidBody|World|KinematicCharacterController)Handle\b/u,
  /\bRAPIER\.(?:Collider|RigidBody|World|KinematicCharacterController)\b/u,
]

const COLLISION_FORBIDDEN_GLOBALS = [
  /\bwindow\b/u,
  /\bdocument\b/u,
  /\bnavigator\b/u,
  /\brequestAnimationFrame\b/u,
]

const SNAPSHOT_MUTATIONS = [
  /\b(?:snapshot|simulationSnapshot|playableSnapshot)(?:\??\.[A-Za-z_$][\w$]*)+\s*(?:=(?!=|>)|\+=|-=|\*=|\/=|\+\+|--)/u,
  /\bdelete\s+(?:snapshot|simulationSnapshot|playableSnapshot)(?:\??\.[A-Za-z_$][\w$]*)+/u,
  /\b(?:Object\.assign|Reflect\.set)\s*\(\s*(?:snapshot|simulationSnapshot|playableSnapshot)\b/u,
  /\b(?:snapshot|simulationSnapshot|playableSnapshot)(?:\??\.[A-Za-z_$][\w$]*)*\.(?:push|pop|shift|unshift|splice|sort|reverse|fill|copyWithin)\s*\(/u,
]

const GAMEPLAY_MUTATION_SYMBOL =
  /^(?:step|apply|mutate|advance|dealDamage|buildMovementIntent|runHeadless|runSimulation|runPlayable|resolveShot|resolveDamage|resolveCombat)/u
const GAMEPLAY_MUTATION_CALL =
  /\b(?:step|apply|mutate|advance|dealDamage|buildMovementIntent|runHeadless|runSimulation|runPlayable|resolveShot|resolveDamage|resolveCombat)[A-Za-z0-9_$]*\s*\(/u
const GAMEPLAY_EXECUTION_MODULE =
  /(?:^|\/)simulation\/(?:step|movement|headlessRunner|replay)(?:\.[cm]?[jt]sx?)?$/u

interface ValueImport {
  readonly moduleSpecifier: string
  readonly importedNames: readonly string[]
  readonly namespace: boolean
}

function stripCommentsAndLiterals(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//gu, ' ')
    .replace(/\/\/[^\n\r]*/gu, ' ')
    .replace(/'(?:\\.|[^'\\])*'/gu, "''")
    .replace(/"(?:\\.|[^"\\])*"/gu, '""')
    .replace(/`(?:\\.|[^`\\])*`/gu, '``')
}

function importedModules(source: string): readonly string[] {
  return [...source.matchAll(IMPORT_MODULE_PATTERN)]
    .map((match) => match[1])
    .filter((specifier): specifier is string => specifier !== undefined)
}

function importedValueBindings(source: string): readonly ValueImport[] {
  return [...source.matchAll(VALUE_IMPORT_PATTERN)].map((match) => {
    const clause = match[1] ?? ''
    const moduleSpecifier = match[2] ?? ''
    const namedSection = clause.match(/\{([\s\S]*?)\}/u)?.[1] ?? ''
    const importedNames = namedSection
      .split(',')
      .map((binding) => binding.trim())
      .filter((binding) => binding.length > 0 && !binding.startsWith('type '))
      .map((binding) => binding.split(/\s+as\s+/u)[0] ?? '')
      .filter((binding) => binding.length > 0)
    return {
      moduleSpecifier,
      importedNames,
      namespace: /(?:^|,)\s*\*\s+as\s+/u.test(clause),
    }
  })
}

function moduleSegment(specifier: string, segment: string): boolean {
  return new RegExp(`(?:^|/)${segment}(?:/|$)`, 'u').test(specifier)
}

function violationMessages(
  sourceWithoutCommentsOrLiterals: string,
  patterns: readonly RegExp[],
  label: string,
): readonly string[] {
  return patterns
    .filter((pattern) => pattern.test(sourceWithoutCommentsOrLiterals))
    .map((pattern) => `${label}: ${pattern.source}`)
}

export function findSimulationBoundaryViolations(source: string): readonly string[] {
  const violations: string[] = []
  for (const specifier of importedModules(source)) {
    if (
      specifier === 'three'
      || specifier.startsWith('three/')
      || specifier === 'react'
      || specifier.startsWith('react/')
      || specifier === 'react-dom'
      || specifier.startsWith('react-dom/')
      || specifier.startsWith('@dimforge/rapier3d')
      || ['render', 'ui', 'runtime'].some((segment) => moduleSegment(specifier, segment))
    ) {
      violations.push(`forbidden simulation import: ${specifier}`)
    }
    if (
      moduleSegment(specifier, 'input')
      && !/(?:^|\/)input\/actionFrame(?:\.[cm]?[jt]sx?)?$/u.test(specifier)
    ) {
      violations.push(`physical input dependency in simulation: ${specifier}`)
    }
  }

  const executableSource = stripCommentsAndLiterals(source)
  violations.push(
    ...violationMessages(executableSource, SIMULATION_FORBIDDEN_GLOBALS, 'browser/input global in simulation'),
    ...violationMessages(executableSource, SIMULATION_FORBIDDEN_WALL_CLOCK, 'wall-clock API in simulation'),
    ...violationMessages(executableSource, SIMULATION_FORBIDDEN_BACKEND_TYPES, 'backend type leaked into simulation'),
  )
  return violations
}

export function findCollisionBoundaryViolations(source: string): readonly string[] {
  const violations: string[] = []
  for (const specifier of importedModules(source)) {
    if (
      specifier === 'three'
      || specifier.startsWith('three/')
      || specifier === 'react'
      || specifier.startsWith('react/')
      || specifier === 'react-dom'
      || specifier.startsWith('react-dom/')
      || ['render', 'ui', 'input'].some((segment) => moduleSegment(specifier, segment))
    ) {
      violations.push(`forbidden collision import: ${specifier}`)
    }
  }
  violations.push(
    ...violationMessages(
      stripCommentsAndLiterals(source),
      COLLISION_FORBIDDEN_GLOBALS,
      'browser global in collision',
    ),
  )
  return violations
}

export function findInputBoundaryViolations(source: string): readonly string[] {
  const violations = importedModules(source)
    .filter((specifier) => moduleSegment(specifier, 'simulation') || moduleSegment(specifier, 'snapshot'))
    .map((specifier) => `snapshot/simulation import in physical input: ${specifier}`)
  violations.push(
    ...violationMessages(
      stripCommentsAndLiterals(source),
      SNAPSHOT_MUTATIONS,
      'snapshot mutation in physical input',
    ),
  )
  return violations
}

export function findPresentationBoundaryViolations(source: string): readonly string[] {
  const violations: string[] = []
  for (const specifier of importedModules(source)) {
    if (GAMEPLAY_EXECUTION_MODULE.test(specifier)) {
      violations.push(`gameplay execution module in presentation: ${specifier}`)
    }
  }
  for (const match of source.matchAll(DYNAMIC_OR_REQUIRE_IMPORT_PATTERN)) {
    const specifier = match[1]
    if (
      specifier !== undefined
      && (moduleSegment(specifier, 'simulation') || moduleSegment(specifier, 'combat'))
    ) {
      violations.push(`dynamic gameplay import in presentation: ${specifier}`)
    }
  }
  for (const valueImport of importedValueBindings(source)) {
    if (
      valueImport.namespace
      && (moduleSegment(valueImport.moduleSpecifier, 'simulation')
        || moduleSegment(valueImport.moduleSpecifier, 'combat'))
    ) {
      violations.push(`gameplay namespace import in presentation: ${valueImport.moduleSpecifier}`)
    }
    for (const importedName of valueImport.importedNames) {
      if (GAMEPLAY_MUTATION_SYMBOL.test(importedName)) {
        violations.push(`gameplay mutation import in presentation: ${importedName}`)
      }
    }
  }

  const executableSource = stripCommentsAndLiterals(source)
  if (GAMEPLAY_MUTATION_CALL.test(executableSource)) {
    violations.push(`gameplay mutation call in presentation: ${GAMEPLAY_MUTATION_CALL.source}`)
  }
  violations.push(
    ...violationMessages(
      executableSource,
      SNAPSHOT_MUTATIONS,
      'snapshot mutation in presentation',
    ),
  )
  return violations
}

function listTypeScriptFiles(directory: string): readonly string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) {
      return entry.name === 'fixtures' ? [] : listTypeScriptFiles(path)
    }
    return ['.ts', '.tsx', '.mts'].includes(extname(entry.name)) && !entry.name.includes('.test.')
      ? [path]
      : []
  })
}

function productionViolations(
  directories: readonly string[],
  audit: (source: string) => readonly string[],
): readonly string[] {
  return directories.flatMap((directory) =>
    listTypeScriptFiles(directory).flatMap((path) =>
      audit(readFileSync(path, 'utf8')).map((violation) => `${path}: ${violation}`),
    ),
  )
}

const sourceRoot = join(dirname(fileURLToPath(import.meta.url)), '..')

describe('граница simulation package', () => {
  it('допускает data-only contracts и canonical ActionFrame', () => {
    const fixture = [
      "import type { CollisionVector3 } from '../collision/contracts'",
      "import type { ActionFrame } from '../input/actionFrame'",
      "import { deepFreeze } from '../core/deepFreeze'",
      'export const next = (frame: ActionFrame) => deepFreeze({ tick: frame.tick })',
    ].join('\n')

    expect(findSimulationBoundaryViolations(fixture)).toEqual([])
  })

  it('обнаруживает Rapier, presentation, physical input, DOM и wall-clock fixtures', () => {
    const fixtures = [
      "import('@dimforge/rapier3d-deterministic-compat')",
      "import { view } from '../render/firstPersonRenderer'",
      "import { KeyboardMouseAdapter } from '../input/keyboardMouseAdapter'",
      'const canvas: HTMLCanvasElement = document.createElement(\'canvas\')',
      'const now = Date.now()',
    ]
    for (const fixture of fixtures) {
      expect(findSimulationBoundaryViolations(fixture), fixture).not.toHaveLength(0)
    }
  })

  it('не допускает backend, browser, input-device, presentation и wall-clock dependencies', () => {
    expect(productionViolations([join(sourceRoot, 'simulation')], findSimulationBoundaryViolations)).toEqual([])
    for (const file of ['planner.ts', 'navigation.ts', 'state.ts']) {
      expect(findSimulationBoundaryViolations(readFileSync(join(sourceRoot, 'ai', file), 'utf8')), file).toEqual([])
    }
  })
})

describe('граница collision package', () => {
  it('допускает data-only и backend imports без presentation/input зависимостей', () => {
    expect(
      findCollisionBoundaryViolations(
        "import type { ArenaDefinition } from '../arena'; import('@dimforge/rapier3d-deterministic-compat')",
      ),
    ).toEqual([])
  })

  it('обнаруживает renderer, React, DOM и input fixtures', () => {
    const fixtures = [
      "import * as THREE from 'three'",
      "import React from 'react'",
      "import { frame } from '../input/actionFrame'",
      "import { view } from '../render/diagnosticRenderer'",
      'document.createElement(\'canvas\')',
    ]
    for (const fixture of fixtures) {
      expect(findCollisionBoundaryViolations(fixture), fixture).not.toHaveLength(0)
    }
  })

  it('не допускает presentation, DOM и input dependencies', () => {
    expect(productionViolations([join(sourceRoot, 'collision')], findCollisionBoundaryViolations)).toEqual([])
  })
})

describe('граница physical input package', () => {
  it('допускает DOM state mapping в canonical action frames', () => {
    const fixture = [
      "import { createActionFrame } from './actionFrame'",
      'const held = new Set<string>()',
      'export const drain = (tick: number) => createActionFrame(tick, [])',
    ].join('\n')
    expect(findInputBoundaryViolations(fixture)).toEqual([])
  })

  it('обнаруживает snapshot imports и mutations в fixtures', () => {
    const fixtures = [
      "import type { SimulationSnapshot } from '../simulation/snapshot'",
      'snapshot.player.health -= 10',
      'Object.assign(playableSnapshot, { tick: 2 })',
      'simulationSnapshot.events.push(event)',
    ]
    for (const fixture of fixtures) {
      expect(findInputBoundaryViolations(fixture), fixture).not.toHaveLength(0)
    }
  })

  it('не импортирует и не мутирует simulation snapshot', () => {
    expect(productionViolations([join(sourceRoot, 'input')], findInputBoundaryViolations)).toEqual([])
  })
})

describe('граница renderer и UI', () => {
  it('допускает type-only snapshot observation', () => {
    const fixture = [
      "import type { SimulationSnapshot } from '../simulation'",
      'export const health = (snapshot: SimulationSnapshot) => snapshot.player.health',
    ].join('\n')
    expect(findPresentationBoundaryViolations(fixture)).toEqual([])
  })

  it('обнаруживает gameplay step imports, calls и snapshot mutations', () => {
    const fixtures = [
      "import { stepSimulation } from '../simulation'",
      "import { buildMovementIntent } from '../simulation/movement'",
      "const rules = await import('../simulation/step')",
      'stepPlayableSimulation(snapshot, frame, collision, profile)',
      'snapshot.weapon.ammo -= 1',
    ]
    for (const fixture of fixtures) {
      expect(findPresentationBoundaryViolations(fixture), fixture).not.toHaveLength(0)
    }
  })

  it('не импортирует и не вызывает gameplay mutations/step', () => {
    expect(
      productionViolations(
        [join(sourceRoot, 'render'), join(sourceRoot, 'ui')],
        findPresentationBoundaryViolations,
      ),
    ).toEqual([])
  })
})
