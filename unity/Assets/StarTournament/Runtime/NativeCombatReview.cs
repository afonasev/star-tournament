#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround
{
    /// <summary>Explicit CLI-only synthetic Player evidence. Weapon evidence uses real seat input/combat; no-killer presentation uses explicit world-damage fixture.</summary>
    public sealed class NativeCombatReview : MonoBehaviour
    {
        readonly Gamepad[] pads = new Gamepad[4];
        ProvingGround ground;
        string directory;
        [Serializable] sealed class ReviewState
        {
            public string scenario="native-combat-review-v1", acceptance="DIAGNOSTIC_NOT_PHYSICAL_ACCEPTANCE";
            public string state, movementProfile, combatProfile, lifeProfile;
            public int width, height, cameras, shots;
            public float mouseDegreesPerPixel;
            public double sessionTime;
            public bool focused, running;
            public CombatLifeState[] lives;
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs(); int flag=Array.IndexOf(args,"-combatEvidence");
            directory=flag>=0 && flag+1<args.Length ? args[flag+1] : Path.Combine(Application.persistentDataPath,"combat-review");
            Directory.CreateDirectory(directory);
            Application.runInBackground=true; // QA harness must finish its bounded synthetic sequence after window focus changes.
            bool shotFeedback=Array.IndexOf(args,"-shotFeedbackReview")>=0;
            bool oneViewport=Array.IndexOf(args,"-oneViewportReview")>=0;
            // Review timings/positions below are scenario fixtures, not shipping gameplay tuning.
            while(!Application.isFocused) yield return null;
            yield return new WaitForSecondsRealtime(1);
            if(shotFeedback)
            {
                PlayerPrefs.DeleteKey(MouseSensitivityPreference.Key);PlayerPrefs.Save();yield return null;yield return Capture("sensitivity-default");
                MouseSensitivityPreference.Set(ground.Profile,MouseSensitivityPreference.Descriptor(ground.Profile).Minimum);yield return null;yield return Capture("sensitivity-minimum");
                MouseSensitivityPreference.Set(ground.Profile,MouseSensitivityPreference.Descriptor(ground.Profile).Maximum);yield return null;yield return Capture("sensitivity-maximum");
                PlayerPrefs.DeleteKey(MouseSensitivityPreference.Key);PlayerPrefs.Save();yield return null;
            }
            yield return Capture("setup");
            for(int i=0;i<pads.Length;i++) pads[i]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads);
            if(oneViewport)
            {
                var reviewCameras=ground.GetComponentsInChildren<Camera>();
                for(int i=1;i<reviewCameras.Length;i++)reviewCameras[i].enabled=false;
                if(reviewCameras.Length>0)reviewCameras[0].rect=new Rect(0,0,1,1);
            }
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);
            // Eight metres of separation exposes the existing pellet spread in a first-person capture
            // while keeping this QA-only target on the shooter's current aim ray.
            ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,10),0);
            ground.PlaceCombatReviewSeat(2,new Vector3(0,0,3),0);
            ground.PlaceCombatReviewSeat(3,new Vector3(8,0,3),0);
            yield return new WaitForSeconds(.3f);
            yield return Capture("live");
            Debug.Log("NATIVE_COMBAT_REVIEW_SHOT_DISPATCH "+directory);
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});
            yield return new WaitForSeconds(shotFeedback?.12f:.2f);
            yield return Capture(shotFeedback?"pellets-flight":"damage");
            // The prior flight snapshot is intentionally before the bounded flight lifetime; this one
            // must be after it so the contact-only impact is what the evidence records.
            yield return new WaitForSeconds(shotFeedback?.2f:0);
            if(shotFeedback)yield return Capture("pellets-impact");
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            yield return new WaitForSeconds(.8f);
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            yield return Capture("killcam");
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            yield return Capture("pause");
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("paused-timers");
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            yield return new WaitForSeconds(ground.LifeProfile.Get("combat.killcamSeconds")+.4f);
            yield return Capture("respawn");
            // Explicit world-damage fixture covers no-killer camera; it is not presented as a weapon hit.
            ground.Session.ApplyDamage(2,ground.Session.Life(2).Life,ground.LifeProfile.Get("combat.maximumHealth"));
            yield return new WaitForSeconds(.3f);
            yield return Capture("no-killer-orbit");
            yield return new WaitForSeconds(ground.LifeProfile.Get("combat.killcamSeconds")+.2f);
            yield return Capture("complete");
            Debug.Log("NATIVE_COMBAT_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string state)
        {
            yield return new WaitForEndOfFrame();
            var snapshot=new ReviewState { state=state, width=Screen.width, height=Screen.height,cameras=Camera.allCamerasCount, focused=Application.isFocused,
                running=ground.Running, shots=ground.Session.ShotCount, sessionTime=ground.Session.Time,
                movementProfile=ground.Profile.Id+"@"+ground.Profile.Version, combatProfile=ground.CombatProfile.Id+"@"+ground.CombatProfile.Version,
                lifeProfile=ground.LifeProfile.Id+"@"+ground.LifeProfile.Version,mouseDegreesPerPixel=MouseSensitivityPreference.Resolve(ground.Profile), lives=new CombatLifeState[4] };
            for(int i=0;i<4;i++) snapshot.lives[i]=ground.Session.Life(i);
            File.WriteAllText(Path.Combine(directory,state+".json"),JsonUtility.ToJson(snapshot,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,state+".png"));
        }
        void OnDestroy() { foreach(var pad in pads) if(pad!=null && pad.added) InputSystem.RemoveDevice(pad); }
    }
}
#endif
