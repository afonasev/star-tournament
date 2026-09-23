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
            ground=GetComponent<ProvingGround>();directory=ground.CombatBowlReviewDirectory;Directory.CreateDirectory(directory);AudioListener.volume=0;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return Capture("01-setup");
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();ground.StartCombatReview(pads);
            if(!ground.Running)throw new InvalidOperationException("P4 review could not start match");
            yield return Capture("02-four-corner-spawns");
            ground.PlaceCombatReviewSeat(0,new Vector3(-4.5f,4,0),0,90);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,-5),0,0);
            ground.PlaceCombatReviewSeat(2,new Vector3(0,0,13),10,180);
            ground.PlaceCombatReviewSeat(3,new Vector3(-22,.6f,-12),15,90);
            yield return new WaitForSeconds(16);yield return Capture("03-hall-basement-entrances");
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
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,repeat=true,player="native macOS Development Player",note="Scripted setup and views; physical controllers and human playable acceptance remain open."},true));
            Debug.Log("COMBAT_BOWL_P4_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.5f);}
        [Serializable] class Result{public string arena,player,note;public bool muted,repeat;public int width,height;}
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);}
    }
}
