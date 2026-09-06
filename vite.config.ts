import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'
import { profileRepositoryPlugin } from './src/dev/profileRepositoryServer'

export default defineConfig({
  plugins: [react(), profileRepositoryPlugin(process.cwd())],
  test: {
    environment: 'node',
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
})
