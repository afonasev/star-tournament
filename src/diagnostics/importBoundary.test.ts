import { readFileSync, readdirSync } from 'node:fs'
import { dirname, extname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

const FORBIDDEN_IMPORTS = [
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"]three(?:\/[^'"]*)?['"]/u,
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"]react(?:-dom)?(?:\/[^'"]*)?['"]/u,
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"][^'"]*(?:\/render|\/ui|\/runtime)(?:\/[^'"]*)?['"]/u,
]

const FORBIDDEN_GLOBALS = [
  /\bwindow\b/u,
  /\bdocument\b/u,
  /\bnavigator\b/u,
  /\brequestAnimationFrame\b/u,
  /\bperformance\.now\b/u,
  /\bDate\.now\b/u,
]

const COLLISION_FORBIDDEN_IMPORTS = [
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"]three(?:\/[^'"]*)?['"]/u,
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"]react(?:-dom)?(?:\/[^'"]*)?['"]/u,
  /(?:from\s+|import\s*\(|require\s*\(|import\s+)['"][^'"]*(?:\/render|\/ui|\/input)(?:\/[^'"]*)?['"]/u,
]

const COLLISION_FORBIDDEN_GLOBALS = [
  /\bwindow\b/u,
  /\bdocument\b/u,
  /\bnavigator\b/u,
  /\brequestAnimationFrame\b/u,
]

const SIMULATION_FORBIDDEN_BACKEND_TYPES = [
  /(?:from\s+|import\s+)['"]@dimforge\/rapier3d[^'"]*['"]/u,
  /\b(?:Collider|RigidBody|World|KinematicCharacterController)Handle\b/u,
  /\bRAPIER\.(?:Collider|RigidBody|World|KinematicCharacterController)\b/u,
]

export function findSimulationBoundaryViolations(
  source: string,
): readonly string[] {
  const violations: string[] = []

  for (const pattern of FORBIDDEN_IMPORTS) {
    if (pattern.test(source)) {
      violations.push(`forbidden import: ${pattern.source}`)
    }
  }

  for (const pattern of FORBIDDEN_GLOBALS) {
    if (pattern.test(source)) {
      violations.push(`forbidden browser or wall-clock global: ${pattern.source}`)
    }
  }

  return violations
}

export function findCollisionBoundaryViolations(source: string): readonly string[] {
  return [...COLLISION_FORBIDDEN_IMPORTS, ...COLLISION_FORBIDDEN_GLOBALS]
    .filter((pattern) => pattern.test(source))
    .map((pattern) => `forbidden collision dependency: ${pattern.source}`)
}

export function findSimulationBackendLeaks(source: string): readonly string[] {
  return SIMULATION_FORBIDDEN_BACKEND_TYPES
    .filter((pattern) => pattern.test(source))
    .map((pattern) => `backend type leaked into simulation: ${pattern.source}`)
}

function listTypeScriptFiles(directory: string): readonly string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) {
      return listTypeScriptFiles(path)
    }

    return ['.ts', '.tsx', '.mts'].includes(extname(entry.name)) && !entry.name.includes('.test.')
      ? [path]
      : []
  })
}

describe('граница simulation package', () => {
  it('обнаруживает запрещённую renderer-зависимость в fixture', () => {
    const fixtures = [
      "import * as THREE from 'three'",
      "import 'three'",
      "const THREE = await import('three')",
      "const THREE = require('three')",
    ]

    for (const fixture of fixtures) {
      expect(findSimulationBoundaryViolations(fixture)).not.toHaveLength(0)
    }
  })

  it('не допускает browser, renderer, React и wall-clock API в simulation', () => {
    const currentDirectory = dirname(fileURLToPath(import.meta.url))
    const simulationDirectory = join(currentDirectory, '../simulation')
    const violations = listTypeScriptFiles(simulationDirectory).flatMap((path) =>
      findSimulationBoundaryViolations(readFileSync(path, 'utf8')).map(
        (violation) => `${path}: ${violation}`,
      ),
    )

    expect(violations).toEqual([])
  })

  it('не допускает backend handles и Rapier imports в simulation contracts', () => {
    const currentDirectory = dirname(fileURLToPath(import.meta.url))
    const simulationDirectory = join(currentDirectory, '../simulation')
    const violations = listTypeScriptFiles(simulationDirectory).flatMap((path) =>
      findSimulationBackendLeaks(readFileSync(path, 'utf8')).map(
        (violation) => `${path}: ${violation}`,
      ),
    )

    expect(violations).toEqual([])
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

  it('обнаруживает renderer, React, DOM и input dependencies в fixtures', () => {
    const fixtures = [
      "import * as THREE from 'three'",
      "import React from 'react'",
      "import { frame } from '../input/actionFrame'",
      "import { view } from '../render/diagnosticRenderer'",
      'document.createElement(\'canvas\')',
      'window.addEventListener(\'resize\', handler)',
    ]

    for (const fixture of fixtures) {
      expect(findCollisionBoundaryViolations(fixture)).not.toHaveLength(0)
    }
  })

  it('не допускает presentation, DOM и input dependencies в collision production files', () => {
    const currentDirectory = dirname(fileURLToPath(import.meta.url))
    const collisionDirectory = join(currentDirectory, '../collision')
    const violations = listTypeScriptFiles(collisionDirectory).flatMap((path) =>
      findCollisionBoundaryViolations(readFileSync(path, 'utf8')).map(
        (violation) => `${path}: ${violation}`,
      ),
    )

    expect(violations).toEqual([])
  })
})
