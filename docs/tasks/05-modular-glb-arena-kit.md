# 05 — Modular GLB arena kit

## Verification

- Dev stand: `http://127.0.0.1:5187/` (HTTP 200).
- Muted in-app Browser: medium arena rendered its first-person start overlay and the retryable pointer-lock failure surface. The in-app Browser rejected pointer lock, so physical input remains unverified there.
- `npm run typecheck`, focused GLB/renderer/runtime tests, `npm run build`, strict OpenSpec validation and `npm run perf:browser` passed. Portable lifecycle gates passed.

## Evidence

The in-app Browser retained current screenshots for the medium start overlay and pointer-lock retry state. Its host does not provide a filesystem screenshot-export API.
