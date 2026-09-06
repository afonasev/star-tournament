import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'
import { readFileSync } from 'node:fs'
import { profileRepositoryPlugin } from './src/dev/profileRepositoryServer'

function packageVersion(): string {
  const candidate = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version?: unknown }
  return typeof candidate.version === 'string' ? candidate.version : '0.0.0'
}

export default defineConfig(({ command }) => {
  const development = command === 'serve'
  const metadata = {
    version: process.env.VITE_APP_VERSION?.trim() || packageVersion(),
    environment: development ? 'development' : 'production',
    branch: process.env.VITE_GIT_BRANCH?.trim() || 'unknown-branch',
    deployedAt: development ? null : process.env.VITE_DEPLOYED_AT?.trim() || null,
  }
  return {
    define: { __STAR_TOURNAMENT_RELEASE_INFO__: JSON.stringify(metadata) },
    plugins: [react(), profileRepositoryPlugin(process.cwd())],
    test: {
      environment: 'node',
      include: ['src/**/*.test.{ts,tsx}'],
      restoreMocks: true,
    },
  }
})
