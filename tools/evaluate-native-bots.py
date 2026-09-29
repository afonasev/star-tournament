#!/usr/bin/env python3
"""Analyze preregistered native bot matches with paired seed bootstrap."""
import argparse,json,random,statistics
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--protocol',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
protocol=json.loads(a.protocol.read_text());runs=[json.loads(f.read_text()) for f in sorted(a.directory.glob('*.json')) if f.name!='complete.json'];runs=[r for r in runs if 'bots' in r]
controls=all(r['set']=='control' for r in runs);expected_seeds=protocol['control_seeds'] if controls else protocol['tuning_seeds'];expected=len(expected_seeds)*2*2*2
result={'profile':protocol['id']+'@'+str(protocol['version']),'set':'control' if controls else 'tuning','expected_matches':expected,'actual_matches':len(runs),'groups':[],'pass':True,'controls_used_for_tuning':False}
for mode in protocol['modes']:
 for hi,lo in protocol['pairs']:
  title=hi.title()+'>'+lo.title();selected=[r for r in runs if r['mode']==mode and r['pair']==title];high={'hard':2,'normal':1,'easy':0}[hi]
  group={'mode':mode,'pair':title,'matches':len(selected)};seed_blocks=[];wins=0;score_hi=score_lo=damage_hi=damage_lo=0
  coverage={(r['seed'],r['permutation']) for r in selected};complete=coverage=={(s,i) for s in expected_seeds for i in range(2)} and len(selected)==len(coverage)
  for seed in expected_seeds:
   sh=sl=dh=dl=0
   for r in selected:
    if r['seed']!=seed:continue
    if r['seconds']<protocol['seconds']-.1:complete=False
    h=[b['standing'] for b in r['bots'] if b['difficulty']==high];l=[b['standing'] for b in r['bots'] if b['difficulty']!=high]
    hs=sum(b['Score'] for b in h);ls=sum(b['Score'] for b in l);hd=sum(b['DamageDealt'] for b in h);ld=sum(b['DamageDealt'] for b in l)
    wins+=1 if hs>ls else protocol['tie_win_weight'] if hs==ls else 0
    sh+=hs;sl+=ls;dh+=hd;dl+=ld
   seed_blocks.append((dh,dl));score_hi+=sh;score_lo+=sl;damage_hi+=dh;damage_lo+=dl
  share=lambda h,l:h/(h+l) if h+l else .5
  rng=random.Random(0xB075);samples=[]
  for _ in range(protocol['bootstrap_resamples']):
   sample=[rng.choice(seed_blocks) for _ in seed_blocks];samples.append(share(sum(x[0] for x in sample),sum(x[1] for x in sample)))
  samples.sort();metrics={'score_share':share(score_hi,score_lo),'damage_share':share(damage_hi,damage_lo),'win_share':wins/len(selected) if selected else 0,'paired_damage_share_bootstrap_lower95':samples[int(len(samples)*.025)]}
  thresholds=protocol['primary_metrics'];passed=complete and all(metrics[k.removesuffix('_minimum')]>=v for k,v in thresholds.items())
  telemetry={}
  for key in ['shots','rifleShots','shotgunShots','pulseShots','beamStarts','damagingShots','collections','incidentalCollections','objectives','unlocks','refills','switches','rocketRejected','aliveSeconds','idleSeconds','blockedSeconds','heal','armor','speed','boost','weapons']:
   telemetry[key]={level:sum(b.get(key,0) for r in selected for b in r['bots'] if b['difficulty']==d) for level,d in [('high',high),('low',{'normal':1,'easy':0}[lo])]}
  group.update(complete=complete,metrics=metrics,thresholds=thresholds,score_totals=[score_hi,score_lo],damage_totals=[damage_hi,damage_lo],seed_damage_blocks=seed_blocks,damage_share_ci95=[samples[int(len(samples)*.025)],samples[min(len(samples)-1,int(len(samples)*.975))]],telemetry=telemetry,passed=passed)
  result['groups'].append(group);result['pass']&=passed
result['pass']&=len(runs)==expected
a.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['set','actual_matches','expected_matches','pass']}));
for g in result['groups']:print(g['mode'],g['pair'],g['passed'],json.dumps(g['metrics']))
raise SystemExit(0 if result['pass'] else 1)
