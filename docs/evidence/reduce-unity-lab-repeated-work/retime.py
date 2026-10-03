from pathlib import Path
p=Path('unity/Assets/StarTournament/Runtime/Core/DesignLabHistory.cs');s=p.read_text();s=s.replace('var transformWatch=Watch();previous=transform(profile);if(Measure)UnityEngine.Debug.Log($"LAB_TRANSFORM name={transform.Method.Name} id={profile.Id} ms={transformWatch.Elapsed.TotalMilliseconds:F3}");cache.Add(profile,previous);','previous=transform(profile);cache.Add(profile,previous);')
s=s.replace('if(Measure)UnityEngine.Debug.Log($"LAB_FINGERPRINT','if(Environment.GetEnvironmentVariable("STAR_LAB_FINGERPRINT")=="1")UnityEngine.Debug.Log($"LAB_FINGERPRINT')
p.write_text(s)
p=Path('unity/Assets/StarTournament/Runtime/Core/ProvingProfile.cs');s=p.read_text();Path('.local/diagnostics/ProvingProfile.cs.original').write_text(s)
a=s.index('public IReadOnlyList<ProfileValidationIssue> Validate()');b=s.index('return issues;',a)
s=s[:b]+'''if(Environment.GetEnvironmentVariable("STAR_LAB_PROFILE")=="1")Debug.Log($"VALIDATE_PROFILE id={id} descriptors={descriptors.Count} ms={validationWatch.Elapsed.TotalMilliseconds:F3}");
            '''+s[b:]
s=s[:a]+s[a:].replace('var issues = new List<ProfileValidationIssue>();','var validationWatch=System.Diagnostics.Stopwatch.StartNew();\n            var issues = new List<ProfileValidationIssue>();',1)
p.write_text(s)
print('Low-volume validation timing installed; fingerprint separate from timing')
