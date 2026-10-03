from pathlib import Path
p=Path('unity/Assets/StarTournament/Runtime/Core/DesignLabHistory.cs');s=p.read_text();original=Path('.local/diagnostics/DesignLabHistory.cs.original').read_text()
a=s.index('static bool Measure=>');b=s.index('public const string ReleaseId=',a);s=s[:a]+s[b:]
a=s.index('public DesignLabHistory(string path,');b=s.index('LabHistoryFile NewFile()',a)
a0=original.index('public DesignLabHistory(string path,');b0=original.index('LabHistoryFile NewFile()',a0)
s=s[:a]+original[a0:b0]+s[b:]
s=s.replace('var revisionWatch=Watch();','')
for phase in ['revision-registry-hash','revision-clone','revision-copy-readonly','revision-validate']:
 s=s.replace('Stamp(revisionWatch,"'+phase+'");','')
s=s.replace('var commitWatch=Watch();','')
for phase in ['commit-clone','commit-mutation','commit-validate']:
 s=s.replace('Stamp(commitWatch,"'+phase+'");','')
s=s.replace('                    \n                    // Metadata','                    // Metadata')
p.write_text(s)
p=Path('unity/Assets/StarTournament/Runtime/Core/ProvingProfile.cs');s=p.read_text();s=s.replace('var validationWatch=System.Diagnostics.Stopwatch.StartNew();\n            ','');s=s.replace('if(Environment.GetEnvironmentVariable("STAR_LAB_PROFILE")=="1")Debug.Log($"VALIDATE_PROFILE id={id} descriptors={descriptors.Count} ms={validationWatch.Elapsed.TotalMilliseconds:F3}");\n            ','');p.write_text(s)
Path('unity/Assets/StarTournament/Runtime/ProvingGround.cs').write_text(Path('.local/diagnostics/ProvingGround.cs.original').read_text())
print('Temporary instrumentation removed')
