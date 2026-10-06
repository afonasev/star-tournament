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
    /// <summary>CLI-only synthetic native input journey; no physical or performance acceptance.</summary>
    public sealed class NativeLayoutReview : MonoBehaviour
    {
        readonly Gamepad[] pads=new Gamepad[4];
        ProvingGround ground;
        string directory;
        [Serializable] sealed class Evidence
        {
            public string scenario="native-seat-layouts-v1", acceptance="DIAGNOSTIC_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE", state;
            public int width,height,seats,participants,cameras,capsules,eventSystems,listeners,arenas,shots;
            public bool running,focused,muted,persistentStandings;
            public double combatClock;
            public Rect[] viewports;
            public NativeMatchSnapshot match;
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-layoutEvidence");
            directory=flag>=0 && flag+1<args.Length?args[flag+1]:Path.Combine(Application.persistentDataPath,"layout-review");
            Directory.CreateDirectory(directory);
            // Synthetic Player evidence must also run when macOS opens the test window unfocused.
            yield return new WaitForSecondsRealtime(1);
            for(int i=0;i<pads.Length;i++) pads[i]=InputSystem.AddDevice<Gamepad>();
            if(Array.IndexOf(args,"-rifleLayoutReview")>=0)
            {
                foreach(int count in new[]{2,3})
                {
                    ground.StartCombatReview(pads.Take(count).ToArray());
                    yield return new WaitForSeconds(.2f);
                    yield return Capture(count+"-rifle-live");
                    Button("В главное меню").onClick.Invoke();
                }
                Debug.Log("NATIVE_RIFLE_LAYOUT_REVIEW_COMPLETE "+directory);
                yield break;
            }
            ground.AddBot(); // The single-view case still needs a second match participant.
            foreach(int count in new[]{1,4,2,3})
            {
                if(count==4)ground.RemoveBot(0);
                ground.StartCombatReview(pads.Take(count).ToArray());yield return new WaitForSeconds(.2f);
                yield return Capture(count+"-live");
                // Real adapter: movement and View belong only to the last active seat.
                InputSystem.QueueStateEvent(pads[count-1],new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.Select));
                yield return new WaitForSeconds(.2f);yield return Capture(count+"-view");
                InputSystem.QueueStateEvent(pads[count-1],new GamepadState());
                InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return new WaitForSecondsRealtime(.15f);
                InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Capture(count+"-pause");
                Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(.15f);yield return Capture(count+"-repeat");
                if(count==4)
                {
                    var armor=ground.Session.Capture();var life=armor.Lives[0];life.Armor=50;armor.Lives[0]=life;
                    ground.Session.Restore(armor);yield return null;yield return Capture("4-armor");
                    // Fast-forward the data-only match clock; this is an overtime display fixture.
                    while(ground.Session.Match.Phase==NativeMatchPhase.Running)
                    {
                        ground.Session.Match.BeginTick();ground.Session.Match.EndTick();
                    }
                    if(ground.Session.Match.Phase!=NativeMatchPhase.Overtime)throw new InvalidOperationException("Layout review did not reach overtime");
                    yield return null;yield return Capture("4-overtime");
                }
                if(count==3)
                {
                    ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);
                    ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,3.5f),0);
                    ground.PlaceCombatReviewSeat(2,new Vector3(8,0,3),0);
                    for(int attempt=0;attempt<4 && !ground.Session.Life(1).Dead;attempt++)
                    {
                        InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return new WaitForSeconds(.8f);
                        InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return new WaitForSeconds(.15f);
                    }
                    InputSystem.QueueStateEvent(pads[0],new GamepadState());
                    if(!ground.Session.Life(1).Dead) throw new InvalidOperationException("Layout review weapon fixture did not kill");
                    InputSystem.QueueStateEvent(pads[1],new GamepadState().WithButton(GamepadButton.Select));
                    yield return new WaitForSeconds(.1f);yield return Capture("3-killcam-view");
                    InputSystem.QueueStateEvent(pads[1],new GamepadState());
                    // Accelerated native empty ticks: validates result/layout lifecycle, not measured real time.
                    var session=ground.Session;var empty=new LocalAction[count];
                    while(session.Match.Phase==NativeMatchPhase.Running)session.Tick(empty,1f/ground.Profile.Get("simulation.fixedTickHz"));
                    yield return new WaitForFixedUpdate();yield return Capture("3-results");
                    Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(.1f);yield return Capture("3-results-repeat");
                    InputSystem.RemoveDevice(pads[2]);yield return new WaitForSecondsRealtime(.1f);yield return Capture("3-disconnect");
                    InputSystem.AddDevice(pads[2]);yield return new WaitForSecondsRealtime(.1f);
                    Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(.1f);yield return Capture("3-reconnect");
                }
                Button("В главное меню").onClick.Invoke();yield return Capture(count+"-setup");
            }
            ground.StartCombatReview(pads);yield return new WaitForSeconds(.2f);yield return Capture("4-return");
            Debug.Log("NATIVE_LAYOUT_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var cams=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).ToArray();
            var value=new Evidence{state=label,width=Screen.width,height=Screen.height,seats=ground.LocalSeatCount,
                participants=ground.Session.ParticipantCount,cameras=cams.Length,viewports=cams.Select(c=>c.rect).ToArray(),
                capsules=ground.GetComponentsInChildren<CharacterController>().Count(c=>c.enabled),running=ground.Running,
                focused=Application.isFocused,muted=AudioListener.volume==0,combatClock=ground.Session.Time,shots=ground.Session.ShotCount,
                match=ground.Session.Match?.Read(),persistentStandings=ground.transform.Find("native-ui/persistent-standings").gameObject.activeSelf,
                eventSystems=ground.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length,
                listeners=ground.GetComponentsInChildren<AudioListener>(true).Length,arenas=ground.GetComponentsInChildren<ProvingArena>(true).Length};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(value,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void OnDestroy(){foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
    }
}
#endif

#endif
