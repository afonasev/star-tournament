using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    /// <summary>Explicit development capture. Diagnostic framing constants never enter gameplay state.</summary>
    public sealed class NativeCombatBowlReview : MonoBehaviour
    {
        ProvingGround ground; string directory;Gamepad[] pads;
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();directory=ground.CombatBowlReviewDirectory;Directory.CreateDirectory(directory);AudioListener.volume=0;Application.runInBackground=true;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return Capture("01-setup");
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();ground.StartCombatReview(pads,true);
            if(!ground.Running)throw new InvalidOperationException("P4 review could not start match");
            yield return Capture("02-four-corner-spawns");
            ground.PlaceCombatReviewSeat(0,new Vector3(-4.5f,4,0),0,90);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,-5),0,0);
            ground.PlaceCombatReviewSeat(2,new Vector3(0,0,13),10,180);
            ground.PlaceCombatReviewSeat(3,new Vector3(-22,.6f,-12),15,90);
            yield return new WaitForSeconds(16);if(!ground.Running)throw new InvalidOperationException("Review paused before gameplay capture");yield return Capture("03-hall-basement-entrances");
            var playerCameras=ground.GetComponentsInChildren<Camera>();
            var oldRects=playerCameras.Select(c=>c.rect).ToArray();
            for(int i=1;i<playerCameras.Length;i++)playerCameras[i].enabled=false;
            playerCameras[0].rect=new Rect(0,0,1,1);yield return Capture("03a-single-viewport");
            for(int i=0;i<playerCameras.Length;i++){playerCameras[i].rect=oldRects[i];playerCameras[i].enabled=true;}
            var samples=new System.Collections.Generic.List<float>();
            for(int frame=0;frame<360;frame++){yield return null;if(!ground.Running)throw new InvalidOperationException("Review paused during timing");samples.Add(Time.unscaledDeltaTime*1000);}
            samples.Sort();
            File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(new Performance{samples=samples.Count,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95f)],allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),width=Screen.width,height=Screen.height,localLights=6,note="Diagnostic on current Mac; four native cameras, scripted stationary views, vsync/frame cap included. Not reference hardware acceptance."},true));
            // Park actors outside the architectural shots after the gameplay readability capture.
            for(int seat=0;seat<4;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(26,.6f,-11-seat*2),0,180);
            // Full-screen architectural views are real Player renders; no wall or roof is hidden.
            var reviewCameras=ground.GetComponentsInChildren<Camera>();var reviewCanvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var c in reviewCameras)c.enabled=false;foreach(var c in reviewCanvases)c.enabled=false;
            var artCameraObject=new GameObject("Orbital architectural capture");var artCamera=artCameraObject.AddComponent<Camera>();artCamera.fieldOfView=72;artCamera.nearClipPlane=.05f;
            var positions=new[]{new Vector3(0,5.65f,-6.8f),new Vector3(0,.45f,-5.7f),new Vector3(-26,2.25f,-12),new Vector3(-16,1.65f,-7),new Vector3(-9,3.8f,4),new Vector3(-10,1.65f,17),new Vector3(10,3.8f,-4),new Vector3(-17,2.4f,-3),new Vector3(-25,2.2f,-16),new Vector3(-1.3f,1.1f,-5)};
            var targets=new[]{new Vector3(0,5.4f,3),new Vector3(0,.6f,7),new Vector3(-20,1.6f,-12),new Vector3(-16,1.65f,6),new Vector3(-4,5.6f,4),new Vector3(-2,1.65f,13),new Vector3(5,5.6f,-4),new Vector3(-19.96f,2.6f,0),new Vector3(-27.94f,2.8f,-17),new Vector3(-2.59f,1.3f,-5)};
            var names=new[]{"06-hall","07-basement","08-spawn","09-west-flank","10-west-rise","11-north-flank","12-east-rise","13-west-screen-oblique","14-spawn-marking","15-enclosed-fan"};
            for(int i=0;i<positions.Length;i++){artCamera.transform.position=positions[i];artCamera.transform.LookAt(targets[i]);yield return Capture(names[i]);}
            Destroy(artCameraObject);foreach(var c in reviewCameras)c.enabled=true;foreach(var c in reviewCanvases)c.enabled=true;
            // Real player projection, camera-only cutaway: roof/wall renderers hidden only for the labelled overview.
            var arena=ground.GetComponentInChildren<ProvingArena>();var cameras=ground.GetComponentsInChildren<Camera>();var canvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var camera in cameras)camera.enabled=false;foreach(var canvas in canvases)canvas.enabled=false;
            var hidden=arena.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("roof")||r.name.Contains("ceiling")).ToArray();foreach(var r in hidden)r.enabled=false;
            var go=new GameObject("P4-cutaway-camera");var overview=go.AddComponent<Camera>();overview.transform.position=new Vector3(42,58,-48);overview.transform.LookAt(new Vector3(0,0,0));overview.fieldOfView=52;overview.backgroundColor=new Color(.1f,.14f,.2f);overview.clearFlags=CameraClearFlags.SolidColor;
            yield return Capture("04-diagnostic-cutaway");
            foreach(var r in hidden)r.enabled=true;Destroy(go);foreach(var camera in cameras)camera.enabled=true;foreach(var canvas in canvases)canvas.enabled=true;
            var old=ground.Session;var repeat=ground.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<Text>()?.text=="Повторить матч");repeat.onClick.Invoke();yield return null;
            if(ground.Session==old||ground.Session.ArenaIdentity!=CombatBowlCatalog.Identity)throw new InvalidOperationException("P4 Repeat identity mismatch");
            yield return Capture("05-repeat");
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,repeat=true,player="native macOS Development Player",note="Scripted live diagnostic match and views; physical controllers and human playable acceptance remain open."},true));
            Debug.Log("COMBAT_BOWL_P4_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.5f);}
        [Serializable] class Performance{public int samples,width,height,localLights;public long allocatedMemoryBytes;public float medianMs,p95Ms;public string note;}
        [Serializable] class Result{public string arena,player,note;public bool muted,repeat;public int width,height;}
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);}
    }
}
