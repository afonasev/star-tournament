using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    // Supporting Editor CPU/allocation probe; never presented as Player FPS.
    public static class F23SurfaceAllocationReview
    {
        [Serializable] sealed class Phase { public string file;public bool cached,allocationCounterAvailable;public long allocatedBytes;public double wallMs; }
        [Serializable] sealed class Report {public string protocol="Actual runtime surface code; one optimized asset only; 24 shots per phase, uncached/cached/cached/uncached order; managed bytes include identical JSON/output overhead. Editor diagnostic, not Player FPS.";public List<Phase> phases=new List<Phase>();}
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("STAR_F23_CPU_REVIEW");if(!Path.IsPathFullyQualified(directory??""))throw new InvalidOperationException("Owned absolute output required");Directory.CreateDirectory(directory);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperBody.prefab");if(!prefab)throw new InvalidOperationException("Prepared body missing");
            var body=UnityEngine.Object.Instantiate(prefab);var visual=body.AddComponent<TrooperVisual>();
            try
            {
                var clips=TrooperVisual.ClipNames.Select(name=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+name+".anim")).ToArray();
                visual.EquippedWeapon=WeaponId.Shotgun;visual.Initialize(clips,ProvingProfile.CreateTrooperDefault(),false);
                for(int i=0;i<=35;i++)visual.Render(i*.01,Vector3.zero,100,1);
                var capture=typeof(NativeModelPerformanceReview).GetMethod("CaptureContactWork",BindingFlags.Static|BindingFlags.NonPublic);if(capture==null)throw new MissingMethodException("CaptureContactWork");
                var report=new Report();int index=0;
                foreach(bool cached in new[]{false,true,true,false})
                {
                    string file=(index++)+(cached?"-cached":"-uncached")+".json";
                    long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
                    capture.Invoke(null,new object[]{body,Vector3.zero,Path.Combine(directory,file),cached});watch.Stop();
                    long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                    report.phases.Add(new Phase{file=file,cached=cached,allocationCounterAvailable=allocated>0,allocatedBytes=allocated,wallMs=watch.Elapsed.TotalMilliseconds});
                }
                File.WriteAllText(Path.Combine(directory,"allocation.json"),JsonUtility.ToJson(report,true));UnityEngine.Debug.Log("F23_SURFACE_ALLOCATION_COMPLETE "+directory);
            }
            finally{visual.Release();UnityEngine.Object.DestroyImmediate(body);}
        }
    }
}
