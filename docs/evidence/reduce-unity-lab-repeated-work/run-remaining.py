from pathlib import Path
import subprocess,os,json,xml.etree.ElementTree as E
root=Path.cwd();out=root/'.local/diagnostics/remaining';out.mkdir(parents=True,exist_ok=True)
editor='/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity'
plan=[('after-play','PlayMode','ShotOriginTests;NativeSeatLayoutTests.SharedTimerAndPerSeatHud;DesignLabWorkspaceTests',3,False),('after-fingerprint','EditMode','NativeMusicTests.PreviousLabRevision;DesignLabHistoryTests.MigrationCacheSeparates',4,True)]
for label,platform,filter,count,fingerprint in plan:
 env=dict(os.environ,STAR_LAB_PROFILE='1',STAR_LAB_FINGERPRINT='1' if fingerprint else '0')
 command=[editor,'-batchmode','-nographics','-projectPath',str(root/'unity'),'-runTests','-testPlatform',platform,'-testFilter',filter,'-testResults',str(out/(label+'.xml')),'-logFile',str(out/(label+'.log'))]
 print('RUN '+label,flush=True)
 result=subprocess.run(command,env=env)
 r=E.parse(out/(label+'.xml')).getroot();cases=r.findall('.//test-case')
 record={'command':command,'exit_code':result.returncode,'root':r.attrib,'count':len(cases),'expected_count':count,'exclusive':True,'instrumented':True,'fingerprint_enabled':fingerprint}
 (out/(label+'.json')).write_text(json.dumps(record,indent=2)+'\n')
 if result.returncode or r.get('result')!='Passed' or len(cases)!=count or any(c.get('result')!='Passed' for c in cases):raise SystemExit(label+' failed/zero/incomplete')
 print(label+' Passed '+str(len(cases))+' duration='+r.get('duration'),flush=True)
