from pathlib import Path
import re
root=Path.cwd();lab=root/'unity/Assets/StarTournament/Runtime/Core/DesignLabHistory.cs';scene=root/'unity/Assets/StarTournament/Runtime/ProvingGround.cs'
for file in [lab,scene]:
 s=file.read_text();(root/'.local/diagnostics'/ (file.name+'.original')).write_text(s)
s=lab.read_text()
# Diagnostic functions deliberately temporary; timings exclude the JSON equivalence transcript.
s=s.replace('public const string ReleaseId=', '''static bool Measure=>Environment.GetEnvironmentVariable("STAR_LAB_PROFILE")=="1";
        static System.Diagnostics.Stopwatch Watch()=>System.Diagnostics.Stopwatch.StartNew();
        static void Stamp(System.Diagnostics.Stopwatch watch,string phase){if(Measure)UnityEngine.Debug.Log($"LAB_PROFILE phase={phase} ms={watch.Elapsed.TotalMilliseconds:F3}");watch.Restart();}
        static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
        static string Fingerprint(LabBundle[] bundles){using(var sha=SHA256.Create()){foreach(var b in bundles){var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(b)+"\\n");sha.TransformBlock(bytes,0,bytes.Length,null,0);}sha.TransformFinalBlock(Array.Empty<byte>(),0,0);return BitConverter.ToString(sha.Hash).Replace("-","");}}
        public const string ReleaseId=''')
s=s.replace('this.path=path;this.shipped=shipped.Clone();this.atomicPublish=atomicPublish??Publish;', 'var ctorWatch=Watch();this.path=path;this.shipped=shipped.Clone();this.atomicPublish=atomicPublish??Publish;Stamp(ctorWatch,"ctor-clone");')
s=s.replace('data=NewFile();','data=NewFile();Stamp(ctorWatch,"ctor-newfile");',1)
s=s.replace('ValidateFile(loaded);data=loaded;','Stamp(ctorWatch,"ctor-read");ValidateFile(loaded);Stamp(ctorWatch,"ctor-validate");data=loaded;')
s=s.replace('Commit(MigrateCompatibility);','Commit(MigrateCompatibility);Stamp(ctorWatch,"ctor-migrate-commit");',1)
s=s.replace('if(predecessors!=null)return predecessors;', 'if(predecessors!=null){if(Measure)UnityEngine.Debug.Log("LAB_CACHE instance-hit");return predecessors;}')
s=s.replace('if(historicalCache.TryGetValue(key,out var cached))return cached;', 'if(historicalCache.TryGetValue(key,out var cached)){if(Measure)UnityEngine.Debug.Log("LAB_CACHE global-hit key="+Digest(key));return cached;}\n                var genWatch=Watch();if(Measure)UnityEngine.Debug.Log("LAB_CACHE miss key="+Digest(key));')
# Count and time pure transformations including JSON cloning.
s=s.replace('previous=transform(profile);cache.Add(profile,previous);','var transformWatch=Watch();previous=transform(profile);if(Measure)UnityEngine.Debug.Log($"LAB_TRANSFORM name={transform.Method.Name} id={profile.Id} ms={transformWatch.Elapsed.TotalMilliseconds:F3}");cache.Add(profile,previous);')
s=s.replace('historicalCache.Add(key,predecessors);', 'Stamp(genWatch,"generation");if(Measure)UnityEngine.Debug.Log($"LAB_FINGERPRINT key={Digest(key)} count={predecessors.Length} json={Fingerprint(predecessors)}");historicalCache.Add(key,predecessors);')
lab.write_text(s)
s=scene.read_text();s=s.replace('void Start()\n        {','''void Start()
        {
            var startWatch=System.Diagnostics.Stopwatch.StartNew();
            void StartStamp(string phase){if(Environment.GetEnvironmentVariable("STAR_LAB_PROFILE")=="1")Debug.Log($"SCENE_PROFILE phase={phase} ms={startWatch.Elapsed.TotalMilliseconds:F3}");startWatch.Restart();}''')
for marker,name in [('InitializeDesignLab();','lab'),('previewMovement=Copy(Profile);','profiles'),('BuildSelectedArena();','arena'),('RebuildActors(Composition);','actors'),('phase=Phase.MainMenu;RefreshInterface();Select(mainBattleButton);','ui')]:
 if name=='profiles':s=s.replace(marker,'StartStamp("profiles-before");'+marker,1)
 else:s=s.replace(marker,marker+'StartStamp("'+name+'");',1)
scene.write_text(s)
print('Temporary diagnostics installed')
