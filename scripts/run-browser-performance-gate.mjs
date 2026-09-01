import { spawn } from 'node:child_process'
import { access, mkdtemp, rm } from 'node:fs/promises'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const delay = (milliseconds) => new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds))

async function availablePath(candidates) {
  for (const candidate of candidates) {
    if (!candidate) continue
    try {
      await access(candidate)
      return candidate
    } catch {
      // Try the next documented browser location.
    }
  }
  throw new Error(
    'Chromium browser not found. Set STAR_TOURNAMENT_CHROME_PATH to a Chrome/Chromium executable.',
  )
}

async function reservePort() {
  const server = createServer()
  await new Promise((resolveListen, reject) => {
    server.once('error', reject)
    server.listen(0, '127.0.0.1', resolveListen)
  })
  const address = server.address()
  if (address === null || typeof address === 'string') throw new Error('Unable to reserve a TCP port')
  await new Promise((resolveClose, reject) => server.close((error) => (
    error ? reject(error) : resolveClose()
  )))
  return address.port
}

async function waitForHttp(url, processHandle) {
  const deadline = Date.now() + 15_000
  while (Date.now() < deadline) {
    if (processHandle.exitCode !== null) {
      throw new Error(`Vite preview exited early with code ${processHandle.exitCode}`)
    }
    try {
      const response = await fetch(url)
      if (response.ok) return
    } catch {
      // Server is still starting.
    }
    await delay(100)
  }
  throw new Error(`Vite preview did not become ready at ${url}`)
}

async function devToolsWebSocket(processHandle) {
  return new Promise((resolveSocket, reject) => {
    let stderr = ''
    const timeout = setTimeout(() => reject(new Error('Chromium DevTools endpoint timed out')), 15_000)
    const onExit = (code) => {
      clearTimeout(timeout)
      reject(new Error(`Chromium exited before DevTools was ready (code ${code})`))
    }
    processHandle.once('exit', onExit)
    processHandle.stderr.on('data', (chunk) => {
      stderr += chunk.toString()
      const match = stderr.match(/DevTools listening on (ws:\/\/[^\s]+)/)
      if (!match) return
      clearTimeout(timeout)
      processHandle.off('exit', onExit)
      resolveSocket(match[1])
    })
  })
}

class CdpClient {
  constructor(url) {
    this.socket = new WebSocket(url)
    this.nextId = 1
    this.pending = new Map()
    this.socket.addEventListener('message', (event) => {
      const message = JSON.parse(typeof event.data === 'string' ? event.data : String(event.data))
      if (message.id === undefined) return
      const pending = this.pending.get(message.id)
      if (!pending) return
      this.pending.delete(message.id)
      if (message.error) pending.reject(new Error(message.error.message))
      else pending.resolve(message.result)
    })
  }

  async connect() {
    if (this.socket.readyState === WebSocket.OPEN) return
    await new Promise((resolveOpen, reject) => {
      this.socket.addEventListener('open', resolveOpen, { once: true })
      this.socket.addEventListener('error', reject, { once: true })
    })
  }

  send(method, params = {}, sessionId) {
    const id = this.nextId++
    const promise = new Promise((resolveResult, reject) => {
      this.pending.set(id, { resolve: resolveResult, reject })
    })
    this.socket.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }))
    return promise
  }

  close() {
    this.socket.close()
  }
}

async function readPerformanceReport(client, sessionId) {
  const deadline = Date.now() + 45_000
  while (Date.now() < deadline) {
    const evaluation = await client.send('Runtime.evaluate', {
      expression: `({
        status: document.querySelector('[data-performance-benchmark-status]')?.dataset.performanceBenchmarkStatus,
        error: document.querySelector('[data-performance-benchmark-error]')?.dataset.performanceBenchmarkError,
        report: window.__STAR_TOURNAMENT_PERFORMANCE_REPORT__ ?? null
      })`,
      returnByValue: true,
    }, sessionId)
    const value = evaluation.result?.value
    if (value?.status === 'failed' && value?.report === null) {
      throw new Error(`Browser performance gate failed before report: ${value.error ?? 'unknown error'}`)
    }
    if (value?.report) return value.report
    await delay(200)
  }
  throw new Error('Browser performance report timed out')
}

async function startDefaultMatch(client, sessionId) {
  const deadline = Date.now() + 10_000
  while (Date.now() < deadline) {
    const evaluation = await client.send('Runtime.evaluate', {
      expression: `(() => {
        const button = [...document.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === 'Начать матч')
        if (!button) return false
        button.click()
        return true
      })()`,
      returnByValue: true,
    }, sessionId)
    if (evaluation.result?.value === true) return
    await delay(100)
  }
  throw new Error('Pre-match menu did not become ready')
}

const previewPort = await reservePort()
const previewUrl = `http://127.0.0.1:${previewPort}/?performanceBenchmark=1`
const chromePath = await availablePath([
  process.env.STAR_TOURNAMENT_CHROME_PATH,
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/Applications/Chromium.app/Contents/MacOS/Chromium',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
])
const browserProfile = await mkdtemp(join(tmpdir(), 'star-tournament-perf-'))
const viteBinary = join(projectRoot, 'node_modules', '.bin', 'vite')
const preview = spawn(viteBinary, [
  'preview',
  '--host', '127.0.0.1',
  '--port', String(previewPort),
  '--strictPort',
], { cwd: projectRoot, stdio: ['ignore', 'pipe', 'pipe'] })
let chrome
let client

try {
  await waitForHttp(previewUrl, preview)
  chrome = spawn(chromePath, [
    '--headless=new',
    '--remote-debugging-port=0',
    `--user-data-dir=${browserProfile}`,
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-background-networking',
    '--disable-component-update',
    '--disable-sync',
    '--metrics-recording-only',
    '--enable-gpu',
    'about:blank',
  ], { stdio: ['ignore', 'ignore', 'pipe'] })
  const devToolsUrl = await devToolsWebSocket(chrome)
  client = new CdpClient(devToolsUrl)
  await client.connect()
  const { targetId } = await client.send('Target.createTarget', { url: 'about:blank' })
  const { sessionId } = await client.send('Target.attachToTarget', { targetId, flatten: true })
  await client.send('Page.enable', {}, sessionId)
  await client.send('Runtime.enable', {}, sessionId)
  await client.send('Emulation.setDeviceMetricsOverride', {
    width: 1280,
    height: 720,
    deviceScaleFactor: 2,
    mobile: false,
  }, sessionId)
  await client.send('Page.navigate', { url: previewUrl }, sessionId)
  await startDefaultMatch(client, sessionId)
  const report = await readPerformanceReport(client, sessionId)
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
  if (!report.passed) process.exitCode = 1
} finally {
  client?.close()
  chrome?.kill('SIGTERM')
  preview.kill('SIGTERM')
  await rm(browserProfile, { recursive: true, force: true })
}
