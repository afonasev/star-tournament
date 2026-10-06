#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Explicit CLI synthetic native journey, not physical or performance acceptance.</summary>
    public sealed class NativeTeamsReview : MonoBehaviour
    {
        ProvingGround ground;
        string directory;
        readonly Gamepad[] pads=new Gamepad[4];
        [Serializable] sealed class Evidence
        {
            public string state,acceptance="DIAGNOSTIC_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE";
            public string teamProfile="unity-native-team-v1@1";
            public int width,height,seats,participants,shots;
            public NativeMatchMode setupMode;
            public bool focused,muted,running;
            public double clock;
            public NativeMatchSnapshot match;
            public CombatLifeState[] lives;
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-teamEvidence");
            directory=i>=0 && i+1<args.Length?args[i+1]:Path.Combine(Application.persistentDataPath,"team-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused)yield return null;
            for(int p=0;p<pads.Length;p++)pads[p]=InputSystem.AddDevice<Gamepad>();
            foreach(int count in new[]{2,3,4})
            {
                while(ground.LocalSeatCount>count)Button("seats-minus").onClick.Invoke();
                while(ground.LocalSeatCount<count)Button("seats-plus").onClick.Invoke();
                ground.SetMatchMode(NativeMatchMode.Teams);
                for(int p=0;p<count;p++)ground.SetTeam(p,NativeTeam.TeamA);
                yield return Capture(count+"-invalid-setup");
                for(int p=0;p<count;p++)ground.SetTeam(p,p==0 || (count==3 && p==2)?NativeTeam.TeamA:NativeTeam.TeamB);
                while(ground.Configuration.DurationMinutes>1)Button("duration-minus").onClick.Invoke();
                if(count==3)Button("team-colors").onClick.Invoke();
                yield return Capture(count+"-setup");
                ground.StartCombatReview(pads.Take(count).ToArray());yield return new WaitForSeconds(.2f);yield return Capture(count+"-live");
                InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Select));yield return new WaitForSeconds(.1f);yield return Capture(count+"-standings");
                InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return new WaitForSecondsRealtime(.1f);
                InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Capture(count+"-pause");
                Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(.1f);yield return Capture(count+"-repeat");
                if(count==3)
                {
                    ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);
                    ground.PlaceCombatReviewSeat(2,new Vector3(-8,0,3.5f),0);
                    ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,5),0);
                    int beforeShots=ground.Session.ShotCount,beforeAmmo=ground.Session.Life(0).Ammo;
                    InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return new WaitForSeconds(.2f);
                    InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Capture("3-ally-blocks");
                    if(!ground.Running || ground.Session.ShotCount!=beforeShots+1 || ground.Session.Life(0).Ammo!=beforeAmmo-1 || ground.Session.Life(1).Health!=100 || ground.Session.Life(2).Health!=100)throw new InvalidOperationException("Allied blocker failed");
                }
                // Resolved diagnostic damage exercises native death/team result adapters, not aiming acceptance.
                ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500,0,ground.Session.Life(0).Life);
                yield return Capture(count+"-killcam");
                var session=ground.Session;
                while(session.Match.Phase==NativeMatchPhase.Running)session.Tick(new LocalAction[count],1f/ground.Profile.Get("simulation.fixedTickHz"));
                yield return new WaitForFixedUpdate();yield return Capture(count+"-results");
                if(session.Match.Read().WinnerTeam!=NativeTeam.TeamA)throw new InvalidOperationException("Wrong team winner");
                Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(.1f);yield return Capture(count+"-result-repeat");
                Button("В главное меню").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Ffa);
                ground.StartCombatReview(pads.Take(count).ToArray());yield return new WaitForSeconds(.1f);yield return Capture(count+"-ffa");
                Button("В главное меню").onClick.Invoke();
            }
            Debug.Log("NATIVE_TEAMS_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;
            yield return new WaitForEndOfFrame();
            var data=new Evidence{state=label,width=Screen.width,height=Screen.height,seats=ground.LocalSeatCount,setupMode=ground.SetupMode,participants=ground.Session.ParticipantCount,
                shots=ground.Session.ShotCount,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,
                clock=ground.Session.Time,match=ground.Session.Match?.Read(),lives=Enumerable.Range(0,ground.Session.ParticipantCount).Select(p=>ground.Session.Life(p)).ToArray()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void OnDestroy(){foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
