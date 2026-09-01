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
})
