#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    /// <summary>Development Player visual evidence; never changes pickup simulation state.</summary>
    public sealed class NativeBonusVisualReview : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Gamepad[] pads;
        IEnumerator Start()
        {
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_BONUS_EVIDENCE");
            if(string.IsNullOrEmpty(directory))throw new InvalidOperationException("Bonus review evidence directory missing");
            Directory.CreateDirectory(directory);
            AudioListener.volume=0;
            Application.runInBackground=true;
            Debug.Log("BONUS_VISUAL_REVIEW_START "+directory);
            var ground=GetComponent<ProvingGround>();
            yield return new WaitForSecondsRealtime(.5f);
            pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};
            ground.StartCombatReview(pads);
            if(!ground.Running)throw new InvalidOperationException("Bonus review match did not start");
            var session=ground.Session;
            bool damage=session.HasDamagePickup, speed=session.HasSpeedPickup;
            if(!damage&&!speed)throw new InvalidOperationException("Review arena has no second pickup");
            var second=damage?session.DamagePickup.Anchor:session.SpeedPickup.Anchor;
            var armor=session.ArmorPickup.Anchor;
            Place(ground,0,armor,5);
            Place(ground,1,second,5);
            // The delayed speed/damage pickup first becomes available after 10/15 simulation seconds.
            yield return new WaitForSeconds(16);
            yield return Capture(directory,"01-distant-two-seats");
            Place(ground,0,armor,2.6f);
            Place(ground,1,second,2.6f);
            yield return Capture(directory,"02-near-two-seats");
            yield return new WaitForSeconds(2);
            yield return Capture(directory,"03-rotated-two-seats");
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonUtility.ToJson(new ReviewResult
            {
                arena=session.ArenaIdentity,secondType=damage?"damage":"speed",muted=AudioListener.volume==0,
                width=Screen.width,height=Screen.height,
                note="Scripted native Player visual diagnostic; human physical-playability acceptance remains open."
            },true));
            Debug.Log("BONUS_VISUAL_REVIEW_COMPLETE "+directory);
        }
        static void Place(ProvingGround ground,int seat,Vector3 anchor,float distance)
        {
            ground.PlaceCombatReviewSeat(seat,anchor+new Vector3(0,0,-distance),5,0);
        }
        static IEnumerator Capture(string directory,string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            yield return new WaitForSecondsRealtime(.6f);
        }
        void OnDestroy()
        {
            if(pads==null)return;
            foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
        }
        [Serializable] sealed class ReviewResult
        {
            public string arena,secondType,note;
            public bool muted;
            public int width,height;
        }
#endif
    }
}

#endif
