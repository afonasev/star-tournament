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
    // Native Player visual evidence with synthetic devices; physical-controller acceptance remains human.
    public sealed class NativeProfilesReview : MonoBehaviour
    {
        ProvingGround ground;
        Gamepad first,second,replacement;
        string directory;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(new ReviewState
            {state=name,width=Screen.width,height=Screen.height,focused=Application.isFocused,muted=AudioListener.volume==0,
                startInteractable=Button("Начать — четыре игрока").interactable},true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        [Serializable] sealed class ReviewState
        {
            public string state,classification="SYNTHETIC_NATIVE_PLAYER_VISUAL_NOT_HUMAN_ACCEPTANCE";
            public int width,height;
            public bool focused,muted,startInteractable;
        }
        IEnumerator Join(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-profilesReview");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"profiles-review");
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("01-main-menu");
            Button("main-action-3").onClick.Invoke();
            if(ground.GetComponentsInChildren<Button>(true).Any(b=>b.name.StartsWith("profile-")&&b.name.Length==40))
            {
                yield return Capture("09-profiles-after-restart");
                File.WriteAllText(Path.Combine(directory,"restart-complete.txt"),"PASS: existing stable profile loaded in a new native Player process.");
                Debug.Log("NATIVE_PROFILES_RESTART_REVIEW_COMPLETE "+directory);
                yield break;
            }
            yield return Capture("02-profiles-empty");
            Button("profile-create").onClick.Invoke();
            var input=ground.GetComponentsInChildren<InputField>(true).Single(f=>f.name=="profile-name-input");
            input.text="QA Алексей";Button("profile-rename").onClick.Invoke();
            yield return Capture("03-profile-created");
            Button("profiles-back").onClick.Invoke();Button("main-action-0").onClick.Invoke();
            yield return Capture("04-setup-unassigned");
            first=InputSystem.AddDevice<Gamepad>();yield return Join(first);
            yield return Capture("05-identity-picker");
            var profileButton=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name.StartsWith("identity-profile-"));
            profileButton.onClick.Invoke();yield return Capture("06-profile-assigned");
            second=InputSystem.AddDevice<Gamepad>();yield return Join(second);
            yield return Capture("07-occupied-profile");
            Button("identity-guest").onClick.Invoke();InputSystem.RemoveDevice(second);yield return null;
            yield return Capture("08-disconnected-device");
            replacement=InputSystem.AddDevice<Gamepad>();yield return Join(replacement);
            var heading=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="identity-heading");
            if(!heading.text.Contains("ИГРОК 2"))throw new InvalidOperationException("Replacement Y did not target disconnected seat 2");
            yield return Capture("10-rebind-disconnected-seat");
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: native Player menu, persisted profile UI, Y picker, occupied profile, disconnected setup device and explicit Y rebind. Synthetic input; no human acceptance.");
            Debug.Log("NATIVE_PROFILES_REVIEW_COMPLETE "+directory);
        }
        void OnDestroy()
        {
            if(first!=null&&first.added)InputSystem.RemoveDevice(first);
            if(second!=null&&second.added)InputSystem.RemoveDevice(second);
            if(replacement!=null&&replacement.added)InputSystem.RemoveDevice(replacement);
        }
    }
}
#endif

#endif
