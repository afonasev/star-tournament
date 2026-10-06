#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround
{
    /// <summary>Explicit synthetic native Player evidence. No human acceptance implied.</summary>
    public sealed class VectorVisualReview : MonoBehaviour
    {
        Gamepad[] pads;string directory;ProvingGround ground;
        IEnumerator Start()
        {
            directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_EVIDENCE");
            if(string.IsNullOrEmpty(directory))throw new InvalidOperationException("Vector evidence path required");
            Directory.CreateDirectory(directory);AudioListener.volume=0;Application.runInBackground=true;
            ground=GetComponent<ProvingGround>();pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};
            yield return new WaitForSecondsRealtime(.5f);
            var roster=NativeMatchRoster.Ffa(2);
            var composition=new NativeMatchComposition(roster,new[]{new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Игрок 1",NativeStandingsView.Palette[0]),new NativeParticipantInfo(NativeParticipantKind.DiagnosticFixture,"Fixture",NativeStandingsView.Palette[1])},new[]{0});
            ground.StartParticipantReview(composition);yield return new WaitForSeconds(.2f);
            if(!ground.transform.Find("seat-camera-1/trooper-view"))throw new InvalidOperationException("Equipment review did not create a first-person view");
            var anchor=ground.Session.Pose(0).Position;
            ground.PlaceCombatReviewSeat(0,anchor,0,45);yield return new WaitForSeconds(.2f);
            yield return Capture("01-idle");
            // Advance the real combat session to exact diagnostic sample times, independent of
            // macOS background throttling and screenshot stalls. No fabricated fire/impact event.
            ground.enabled=false;
            ground.Session.Tick(new[]{new LocalAction{Fire=true},new LocalAction()},Time.fixedDeltaTime);
            ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");
            yield return Capture("02-fire");
            ground.Session.Tick(new LocalAction[2],.14f);ground.SendMessage("LateUpdate");
            yield return Capture("03-smoke");
            ground.Session.Tick(new LocalAction[2],.5f);ground.SendMessage("LateUpdate");
            yield return Capture("04-settled");
            ground.Session.Tick(new[]{new LocalAction{Move=new Vector2(0,1)},new LocalAction()},.1f);ground.SendMessage("LateUpdate");
            yield return Capture("06-motion");ground.enabled=true;
            ground.StartCombatReview(pads,true);yield return new WaitForSeconds(.2f);
            anchor=ground.Session.Pose(0).Position;
            ground.PlaceCombatReviewSeat(0,anchor,0,0);ground.PlaceCombatReviewSeat(1,anchor+Vector3.forward*2,0,180);
            yield return new WaitForSeconds(.3f);yield return Capture("05-two-seats-world");
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"DIAGNOSTIC_NOT_ACCEPTANCE\",\"muted\":true,\"profile\":\"unity-proving-ground-v1@1\",\"equipment\":\"strata-oxblood-equipment-v2-f5\"}");
            Debug.Log("VECTOR_REVIEW_COMPLETE "+directory);
            yield return new WaitForSecondsRealtime(.5f);Application.Quit();
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return null;}
        void OnDestroy(){if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
    }
}
#endif

#endif
