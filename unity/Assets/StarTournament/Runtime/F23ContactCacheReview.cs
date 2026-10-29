#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // Paired CPU diagnostic after an actual hot path was measured. No game-state mutation.
    public sealed class F23ContactCacheReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap()
        {if(Environment.GetCommandLineArgs().Contains("-f23ContactReview")||Environment.GetCommandLineArgs().Contains("-f23WorldReview"))new GameObject("F23 contact CPU diagnostic").AddComponent<F23ContactCacheReview>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-f23WorldReview");bool world=index>=0;if(!world)index=Array.IndexOf(args,"-f23ContactReview");
            string directory=args[index+1];if(!Path.IsPathFullyQualified(directory))throw new InvalidOperationException("Owned absolute path required");Directory.CreateDirectory(directory);
            Application.runInBackground=true;Invoke(nameof(Timeout),180);ProvingGround ground;
            while(!(ground=FindFirstObjectByType<ProvingGround>())||!ground.IsReady||!SplashScreen.isFinished)yield return null;
            if(AudioListener.volume!=0)throw new InvalidOperationException("Muted admission required");
            foreach(var variant in new[]{"original","optimized"})
            {
                var prefab=variant=="original"?Resources.Load<GameObject>("ModelPerformanceBaseline"):ground.TrooperBodyPrefab;
                if(!prefab)throw new InvalidOperationException("Missing body "+variant);
                if(world){yield return NativeModelPerformanceReview.CaptureWorld(ground,prefab,Path.Combine(directory,variant),true,false);continue;}
                var origin=new Vector3(1000,1000,1000);var body=Instantiate(prefab,origin,Quaternion.identity);
                var visual=body.AddComponent<TrooperVisual>();visual.EquippedWeapon=WeaponId.Shotgun;visual.Initialize(ground.TrooperClips,ProvingProfile.CreateTrooperDefault(),false);
                for(int i=0;i<=35;i++)visual.Render(i*.01,Vector3.zero,100,1);
                yield return null;
                foreach(bool cached in new[]{false,true})
                    NativeModelPerformanceReview.CaptureContactWork(body,origin,Path.Combine(directory,variant+(cached?"-cached":"-uncached")+".json"),cached);
                visual.Release();Destroy(body);yield return null;
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),world?"{\"status\":\"COMPLETE_NATIVE_VISUAL_DIAGNOSTIC_NOT_ACCEPTANCE\",\"framesPerBody\":6}":"{\"status\":\"COMPLETE_CPU_DIAGNOSTIC_NOT_ACCEPTANCE\",\"shotsPerVariant\":24}");
            Application.Quit(0);
        }
        void Timeout()=>Application.Quit(2);
    }
}
#endif
