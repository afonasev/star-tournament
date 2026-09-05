/** Test budgets belong to the versioned suite, never to gameplay or RNG. */
export const AI_EVALUATION_V1 = {
  id: 'ai-evaluation-v1',
  calibrationSeeds: [101, 102, 103, 104, 105, 106, 10036, 10060],
  // Earlier holdouts exposed correctness bugs and became regressions. Reserve
  // a fresh unseen range before running the final strength evaluation.
  evaluationSeedStart: 20001,
  pairedSeeds: 100,
  tickBudget: 7200,
  checkpointEveryTicks: 600,
  maximumIdleTicks: 900,
  confidenceZ: 1.959963984540054,
  requiredWinLowerBound: 0.5,
} as const

export function wilsonInterval(wins: number, total: number): [number,number] {
  if(total===0)return [0,1]
  const z=AI_EVALUATION_V1.confidenceZ;const p=wins/total;const scale=1+z*z/total
  const center=(p+z*z/(2*total))/scale
  const spread=z*Math.sqrt(p*(1-p)/total+z*z/(4*total*total))/scale
  return [center-spread,center+spread]
}
