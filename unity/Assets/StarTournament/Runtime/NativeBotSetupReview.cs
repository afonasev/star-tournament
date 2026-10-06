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
    /// <summary>Development evidence through ordinary setup buttons and device joining, never StartBotReview.</summary>
    public sealed class NativeBotSetupReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;
        [Serializable] sealed class Evidence {public string state,status="ORDINARY_SETUP_SYNTHETIC_DEVICES_NOT_PHYSICAL_ACCEPTANCE";public bool focused,muted,running;public int width,height,shots;public uint seed;public double time;public NativeCompositionSnapshot composition;public NativeMatchSnapshot match;}
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool value,string message){if(!value)throw new InvalidOperationException("BOT_SETUP_REVIEW "+message);}
        IEnumerator Join(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
        }
        IEnumerator Capture(string state)
        {
            while(!Application.isFocused)yield return null;
            yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(directory,state+".json"),JsonUtility.ToJson(new Evidence{state=state,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,width=Screen.width,height=Screen.height,shots=ground.Session.ShotCount,seed=ground.BotDriver?.Seed??0,time=ground.Session.Time,composition=ground.Running||ground.Session.Match!=null?ground.Composition.Read():null,match=ground.Session.Match?.Read()},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,state+".png"));yield return null;
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-botSetupEvidence");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"bot-setup-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            for(int i=0;i<3;i++)B("seats-minus").onClick.Invoke();yield return Join(pads[0]);
            Check(!B("Начать — четыре игрока").interactable,"empty solo start enabled");yield return Capture("01-solo-invalid");
            for(int i=0;i<7;i++)B("bot-add").onClick.Invoke();
            for(int i=0;i<7;i++)ground.SetBotDifficulty(i,i%3);
            Check(!B("bot-add").interactable,"limit missing");yield return Capture("02-solo-seven-setup");
            B("Начать — четыре игрока").onClick.Invoke();Check(ground.Running&&ground.BotDriver!=null,"ordinary AI start failed");
            InputSystem.QueueStateEvent(pads[0],new GamepadState{leftStick=Vector2.up,rightStick=Vector2.right});yield return new WaitForSeconds(1);
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return new WaitForSeconds(15);yield return Capture("03-solo-combat");
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Select));yield return new WaitForSeconds(.2f);Check(ground.GetComponentsInChildren<Transform>().Any(t=>t.name=="standings-0"),"live standings missing");yield return Capture("04-solo-standings");
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            Check(!ground.Running,"pause failed");int ticks=ground.BotDriver.Ticks;yield return new WaitForSecondsRealtime(.2f);Check(ticks==ground.BotDriver.Ticks,"AI ticks in pause");yield return Capture("05-paused");
            var frozen=JsonUtility.ToJson(ground.Composition.Read());var driver=ground.BotDriver;B("Повторить матч").onClick.Invoke();yield return null;
            Check(ground.BotDriver!=driver&&JsonUtility.ToJson(ground.Composition.Read())==frozen,"repeat lost composition");yield return Capture("06-repeat");
            ground.SendMessage("Pause","Setup review");B("В главное меню").onClick.Invoke();
            for(int i=0;i<3;i++)B("bot-remove-0").onClick.Invoke();
            for(int i=1;i<4;i++){B("seats-plus").onClick.Invoke();yield return Join(pads[i]);}
            ground.SetMatchMode(NativeMatchMode.Teams);yield return Capture("07-four-humans-four-bots-teams");
            B("Начать — четыре игрока").onClick.Invoke();Check(ground.Running,"teams start failed");yield return new WaitForSeconds(12);yield return Capture("08-teams-four-views");
            ground.SendMessage("Pause","Results review");B("Повторить матч").onClick.Invoke();ground.enabled=false;
            var actions=new LocalAction[8];int budget=(int)(600/Time.fixedDeltaTime);
            for(int i=0;i<budget&&ground.Session.Match.Phase!=NativeMatchPhase.Finished;i++)
            {Array.Clear(actions,0,actions.Length);ground.BotDriver.ProduceActions(actions,Time.fixedDeltaTime);ground.Session.Tick(actions,Time.fixedDeltaTime);if(i%500==0)yield return null;}
            ground.enabled=true;Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"natural result timeout");yield return new WaitForFixedUpdate();yield return null;yield return Capture("09-results");
            B("Повторить матч").onClick.Invoke();Check(ground.Running,"result repeat failed");ground.SendMessage("Pause","Exit review");B("В главное меню").onClick.Invoke();yield return null;Check(ground.BotDriver==null,"driver leaked");yield return Capture("10-return-setup");
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: ordinary UI, synthetic device joins/actions, actual AI, pause, repeat, teams, natural results, cleanup. Physical/TV/performance acceptance open.");Debug.Log("NATIVE_BOT_SETUP_REVIEW_COMPLETE "+directory);
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
