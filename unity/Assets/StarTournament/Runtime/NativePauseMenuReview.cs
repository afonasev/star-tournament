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
    // Development Player evidence only. Synthetic input does not establish physical-controller acceptance.
    public sealed class NativePauseMenuReview : MonoBehaviour
    {
        ProvingGround ground;
        Gamepad[] pads;
        string directory;
        int count;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        Text[] BuildLabels()=>ground.GetComponentsInChildren<Text>(true).Where(t=>t.name=="development-build-label"&&t.gameObject.activeInHierarchy).ToArray();
        IEnumerator Capture(string name)
        {
            var labels=BuildLabels();
            if(labels.Length!=1 || labels[0].rectTransform.anchorMin!=new Vector2(0,1) || labels[0].rectTransform.anchorMax!=new Vector2(0,1))
                throw new InvalidOperationException("Expected one global top-left build label");
            if(UnityEngine.Rendering.Watermark.showDeveloperWatermark)
                throw new InvalidOperationException("Unity development watermark remains enabled");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(new ReviewState
            {
                state=name,localSeats=count,running=ground.Running,matchTime=ground.Session.Time,
                visibleFps=VisibleFpsCount(),
                buildLabels=BuildLabels().Length,unityDeveloperWatermark=UnityEngine.Rendering.Watermark.showDeveloperWatermark,
                openMenus=Enumerable.Range(0,count).Count(i=>Button("pause-action-"+i+"-0").transform.parent.parent.gameObject.activeInHierarchy),
                focused=Application.isFocused,muted=AudioListener.volume==0
            },true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        [Serializable] sealed class ReviewState
        {
            public string state,classification="SYNTHETIC_NATIVE_PLAYER_VISUAL_NOT_HUMAN_ACCEPTANCE";
            public int localSeats,openMenus,visibleFps,buildLabels;
            public bool running,focused,muted,unityDeveloperWatermark;
            public double matchTime;
        }
        int VisibleFpsCount()=>ground.GetComponentsInChildren<Text>(true).Count(t=>
            (t.name=="fps-value"||t.name.StartsWith("seat-fps-"))&&t.gameObject.activeInHierarchy);
        IEnumerator Press(Gamepad pad,GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-pauseReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute pause review directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);
            int countAt=Array.IndexOf(args,"-pauseReviewCount");
            if(countAt<0||countAt+1>=args.Length||!int.TryParse(args[countAt+1],out count)||count<1||count>4)
                throw new ArgumentException("Pause review requires 1–4 local seats");
            yield return new WaitForSecondsRealtime(1);
            if(VisibleFpsCount()!=0)throw new InvalidOperationException("FPS is visible in the main menu");
            yield return Capture("00-main-menu");
            Button("main-action-0").onClick.Invoke();
            if(VisibleFpsCount()!=0)throw new InvalidOperationException("FPS is visible in match setup");
            while(ground.LocalSeatCount>count)Button("seats-minus").onClick.Invoke();
            while(ground.LocalSeatCount<count)Button("seats-plus").onClick.Invoke();
            for(int seat=0;seat<count;seat++)ground.SetSeatAi(seat,false);
            if(count==1)Button("bot-add").onClick.Invoke();
            pads=new Gamepad[count];
            for(int seat=0;seat<count;seat++)pads[seat]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads);yield return null;
            if(!ground.Running)throw new InvalidOperationException("Native pause review match did not start");
            yield return Capture("01-running-"+count);
            for(int seat=0;seat<count;seat++)yield return Press(pads[seat],GamepadButton.Start);
            if(ground.Running)Debug.Log("NATIVE_PAUSE_INPUT_FALLBACK_UNFOCUSED");
            for(int seat=0;seat<count;seat++)
                if(ground.Running||!Button("pause-action-"+seat+"-0").gameObject.activeInHierarchy)ground.OpenPauseReviewSeat(seat);
            if(ground.Running||Enumerable.Range(0,count).Any(i=>!Button("pause-action-"+i+"-0").gameObject.activeInHierarchy))
                throw new InvalidOperationException("Independent viewport menus did not open");
            yield return Capture("02-all-open-"+count);
            if(count==4)
            {
                Button("pause-action-0-1").onClick.Invoke();yield return Capture("03-four-seat-settings");
                Button("pause-setting-0-5").onClick.Invoke();yield return Capture("04-four-seat-help-top");
                var helpScroll=Button("pause-help-pad-0").transform.parent.GetComponentInChildren<ScrollRect>(true);
                Canvas.ForceUpdateCanvases();helpScroll.verticalNormalizedPosition=0;yield return Capture("05-four-seat-help-bottom");
                Button("pause-help-back-0").onClick.Invoke();
            }
            if(count==2)
            {
                Button("pause-action-0-1").onClick.Invoke();yield return Capture("03-seat-one-settings");
                ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="pause-setting-0-0").value+=.01f;var fpsToggle=ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="pause-setting-0-4");fpsToggle.isOn=!fpsToggle.isOn;
                yield return Capture("04-personal-values");
                Button("pause-action-1-3").onClick.Invoke();yield return Capture("05-exit-confirmation");
                Button("pause-exit-no-1").onClick.Invoke();
                Button("pause-setting-0-6").onClick.Invoke();
                Button("pause-action-1-2").onClick.Invoke();yield return Capture("06-repeat-confirmation");
                Button("pause-repeat-yes-1").onClick.Invoke();yield return null;
                if(!ground.Running)throw new InvalidOperationException("Confirmed repeat did not start a fresh match");
                yield return Capture("07-after-repeat");
            }
            if(count==1)
            {
                Button("pause-action-0-3").onClick.Invoke();
                Button("pause-exit-yes-0").onClick.Invoke();
                if(VisibleFpsCount()!=0)throw new InvalidOperationException("FPS remains visible after exiting a match");
                yield return Capture("08-return-main-menu");
            }
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: muted native Player, "+count+" viewport pause menus; synthetic devices, no physical-controller or human acceptance.");
            Debug.Log("NATIVE_PAUSE_REVIEW_COMPLETE "+count+" "+directory);
            if(count==4)Application.Quit();
        }
        void OnDestroy()
        {
            if(pads==null)return;
            foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
        }
    }
}
#endif
