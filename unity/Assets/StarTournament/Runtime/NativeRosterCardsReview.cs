#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Real native Player pixels with synthetic devices/actions; never physical or human acceptance.
    public sealed class NativeRosterCardsReview : MonoBehaviour
    {
        ProvingGround ground;
        string directory;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        Text Text(string name)=>ground.GetComponentsInChildren<Text>(true).Single(t=>t.name==name);
        GameObject Object(string name)=>ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        void Click(string name){var b=Button(name);Check(b.interactable,"Disabled: "+name);b.onClick.Invoke();}
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        SeatInputCoordinator Input=>(SeatInputCoordinator)typeof(ProvingGround).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
        void Invoke(string method,params object[] args)=>typeof(ProvingGround).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(ground,args);
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;
        [Serializable] sealed class State
        {
            public string classification="SYNTHETIC_NATIVE_PLAYER_VISUAL_NOT_HUMAN_ACCEPTANCE",state,focus,mapIdentity,labIdentity,sourceRevision;
            public int width,height,participants,views,reviewSeed=20260928;
            public bool muted,editor;
        }
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(new State{state=name,focus=Focus,sourceRevision=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_SOURCE_REVISION"),mapIdentity=CombatBowlCatalog.Identity,labIdentity=ground.LabSavedIdentity,width=Screen.width,height=Screen.height,participants=ground.LocalSeatCount+ground.SetupBotCount,views=ground.LocalSeatCount,muted=AudioListener.volume==0,editor=Object("roster-editor").activeInHierarchy},true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator Press(Gamepad pad,GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator PressKey(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        }
        void GuestAndDevice(int participant,InputDevice device)
        {
            Click("roster-card-"+participant);Click("roster-identity");Click(participant==0?"roster-choice-profile-qa-roster-alexey":"roster-choice-guest");
            Click("roster-device");Click("roster-choice-device-"+device.deviceId);Click("roster-done");
        }
        void Remove(int p){Click("roster-card-"+p);Click("roster-remove");}
        void ChangeView(int p,bool own){Click("roster-card-"+p);Click("roster-device");Click(own?"roster-choice-view-on":"roster-choice-view-off");Click("roster-done");}
        IEnumerator Start()
        {
            Application.runInBackground=true;
            // Synthetic QA must ignore OS focus changes from concurrent local Player reviews.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.Random.InitState(20260928);ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-rosterCardsReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute roster review directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1f);
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            var pad=InputSystem.AddDevice<Gamepad>();
            EventSystem.current.SetSelectedGameObject(Button("main-action-0").gameObject);
            // Actual menu Submit assigns the pad without a profile prompt or phantom humans.
            yield return Press(pad,GamepadButton.South);Click("setup-next");Click("setup-next");
            Check(ground.LocalSeatCount==1&&ground.SetupBotCount==1&&Input.DeviceAt(0)==pad&&Input.Ready,"Gamepad default roster invalid");
            yield return Capture("00-default-gamepad-one-human-one-bot");
            if(args.Contains("-newBotFocusReview"))
            {
                for(int i=0;i<2;i++)
                {
                    int botCountBefore=ground.SetupBotCount;
                    EventSystem.current.SetSelectedGameObject(Button("roster-add-bot").gameObject);
                    yield return Press(pad,GamepadButton.South);
                    Check(ground.SetupBotCount==botCountBefore+1&&Focus=="roster-done","New bot did not focus Done");
                    Check(ground.SetupComposition().Read().Participants.Last().Difficulty==(int)NativeBotDifficulty.Normal,"New bot default changed");
                    yield return Capture("01-new-bot-done-"+i);
                    yield return Press(pad,GamepadButton.South);
                    Check(!Object("roster-editor").activeInHierarchy&&Focus=="roster-card-"+(ground.LocalSeatCount+botCountBefore),"One A did not confirm bot");
                    yield return Capture("02-confirmed-bot-"+i);
                }
                Click("roster-card-"+(ground.LocalSeatCount+ground.SetupBotCount-1));
                Check(Focus=="roster-identity","Existing bot edit focus changed");
                yield return Capture("03-existing-bot-difficulty");
                InputSystem.RemoveDevice(pad);
                File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: added two Normal bots with synthetic gamepad; initial Done focus and one-A confirmation; existing editor retains difficulty focus. Human physical-controller acceptance pending.");
                Debug.Log("NATIVE_NEW_BOT_FOCUS_REVIEW_COMPLETE "+directory);
                Application.Quit();yield break;
            }

            Invoke("ToMainMenu");
            Canvas.ForceUpdateCanvases();var entry=(RectTransform)Button("main-action-0").transform;
            var entryCenter=RectTransformUtility.WorldToScreenPoint(null,entry.TransformPoint(entry.rect.center));
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=entryCenter});yield return null;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=entryCenter}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=entryCenter});yield return null;
            Click("setup-next");Click("setup-next");
            Check(ground.LocalSeatCount==1&&ground.SetupBotCount==1&&Input.DeviceAt(0)==keyboard&&Input.Ready,"Mouse default roster invalid");
            yield return Capture("00-default-mouse-one-human-one-bot");
            Invoke("ToMainMenu");yield return PressKey(keyboard,Key.Enter);Click("setup-next");Click("setup-next");
            Check(ground.LocalSeatCount==1&&ground.SetupBotCount==1&&Input.DeviceAt(0)==keyboard&&Input.Ready,"Keyboard default roster invalid");
            yield return Capture("00-default-keyboard-one-human-one-bot");
            // Author the two-human fixture explicitly for the established roster coverage below.
            ground.RemoveBot(0);Invoke("SetSeatCount",2);
            GuestAndDevice(0,keyboard);
            // Free gamepad Y assigns an available human view and opens its editor.
            yield return Press(pad,GamepadButton.North);Check(Object("roster-editor").activeInHierarchy,"Y did not open editor");
            Click("roster-identity");Click("roster-choice-guest");Click("roster-done");
            Check(Focus=="roster-card-1","Editor focus did not return");
            yield return Capture("01-ffa-two-humans");
            // Pointer raycast selects the whole card, then keyboard Escape restores it.
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)Button("roster-card-0").transform;
            var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=center});yield return null;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=center}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=center});yield return null;
            Check(Object("roster-editor").activeInHierarchy,"Mouse did not open whole card");
            yield return Capture("02-human-editor");
            yield return PressKey(keyboard,Key.Escape);Check(Focus=="roster-card-0","Keyboard close did not restore card");
            yield return Press(pad,GamepadButton.DpadRight);Check(Focus=="roster-card-1","Gamepad did not navigate grid");
            yield return Press(pad,GamepadButton.South);Check(Focus=="roster-identity","A did not open editor");
            yield return Press(pad,GamepadButton.East);Check(Focus=="roster-card-1","B did not restore focus");
            for(int count=3;count<=8;count++)
            {
                Click("roster-add-bot");Click("roster-identity");Click("roster-choice-difficulty-"+((count-3)%3));Click("roster-done");
                yield return Capture("03-ffa-"+count+"-participants");
            }
            Check(!Button("roster-add-human").interactable&&!Button("roster-add-bot").interactable,"Full roster accepts additions");
            Check(Text("roster-full").text=="Состав заполнен","Full roster reason missing");
            for(int p=2;p<8;p++)
            {
                var card=Object("roster-card-"+p);var labels=card.GetComponentsInChildren<Text>(true);
                Check(labels.Single(t=>t.name=="participant-name").text=="Бот","Bad bot title");
                Check(!labels.Single(t=>t.name=="participant-view").gameObject.activeSelf,"Unassigned view text exists");
            }
            Click("setup-previous");ground.SetMatchMode(NativeMatchMode.Teams);Click("setup-step-2");
            yield return Capture("04-teams-four-four");
            Click("roster-card-2");Click("roster-identity");Click("roster-choice-difficulty-1");
            yield return Capture("05-bot-editor");
            var remove=(RectTransform)Button("roster-remove").transform;var done=(RectTransform)Button("roster-done").transform;
            Check(Math.Abs(remove.rect.width-done.rect.width)<.1f&&Math.Abs(remove.rect.height-done.rect.height)<.1f,"Editor action sizes differ");
            Click("roster-done");Check(Focus=="roster-card-2","Bot focus not restored");
            for(int p=2;p<8;p++)ground.SetBotTeam(p-ground.LocalSeatCount,NativeTeam.TeamA);
            yield return Capture("06-teams-seven-one");
            EventSystem.current.SetSelectedGameObject(Button("roster-card-7").gameObject);yield return null;
            yield return Capture("07-teams-scroll-focus");
            var cardRect=(RectTransform)Button("roster-card-7").transform;var viewport=Object("team-blue-scroll").GetComponent<ScrollRect>().viewport;
            var corners=new Vector3[4];cardRect.GetWorldCorners(corners);
            Check(viewport.InverseTransformPoint(corners[0]).y>=viewport.rect.yMin-2,"Focused card stayed clipped");
            // Explicit team choice migrates the selected card and returns focus to it.
            Click("roster-card-7");Click("roster-team");Click("roster-choice-team-"+(int)NativeTeam.TeamB);Click("roster-done");
            Check(Focus=="roster-card-7","Team transfer lost focus");
            yield return Capture("08-team-move-six-two");
            ChangeView(2,true);ChangeView(3,true);Check(ground.LocalSeatCount==4&&ground.SetupComposition().ParticipantCount==8,"Own screens changed roster count");
            yield return Capture("09-four-human-ai-views");
            Click("roster-card-4");Click("roster-device");Check(!Button("roster-choice-view-on").interactable,"Fifth view is allowed");
            yield return Capture("10-four-view-limit");Click("roster-picker-back");Click("roster-done");
            ChangeView(2,false);Check(ground.LocalSeatCount==3&&ground.SetupComposition().ParticipantCount==8,"View demotion changed count");
            yield return Capture("11-view-demoted");
            InputSystem.DisableDevice(pad);yield return null;yield return null;
            Check(!Button("Начать — четыре игрока").interactable,"Disconnected device did not block start");
            Click("roster-card-1");Check(Text("editor-name").text=="Гость","Disconnect lost identity");Click("roster-done");
            yield return Capture("12-device-lost-identity-kept");
            InputSystem.EnableDevice(pad);yield return null;yield return null;
            Check(Button("Начать — четыре игрока").interactable,"Reconnect did not restore readiness");
            yield return Capture("13-device-returned");
            var replacement=InputSystem.AddDevice<Gamepad>();
            Click("roster-card-1");Click("roster-device");Click("roster-choice-device-"+replacement.deviceId);Click("roster-done");
            Check(Input.DeviceAt(1)==replacement,"Explicit device replacement failed");
            InputSystem.RemoveDevice(replacement);yield return null;yield return null;
            yield return Capture("14-removed-device-blocks-start");
            Click("roster-card-1");Click("roster-device");Click("roster-choice-device-"+pad.deviceId);Click("roster-done");
            int before=ground.SetupComposition().ParticipantCount;
            Click("setup-previous");yield return Capture("15-back-rules");Click("setup-previous");yield return Capture("16-back-map");Click("setup-step-2");
            Check(ground.SetupComposition().ParticipantCount==before&&ground.LocalSeatCount==3,"Back reset draft");yield return Capture("17-draft-restored");
            Remove(0);Check(Input.DeviceAt(0)==pad,"Removing first seat lost next device");
            yield return Capture("18-remove-human-compacts-identity");
            Remove(0);Check(ground.SetupComposition().Read().Participants.All(p=>p.Kind==NativeParticipantKind.Bot),"AI-only composition not possible");
            Check(Input.Ready,"AI-only asks for a device");yield return Capture("19-ai-only");
            Click("roster-add-human");Click("roster-identity");Click("roster-choice-guest");Click("roster-device");Click("roster-choice-device-"+keyboard.deviceId);Click("roster-done");
            yield return Capture("20-added-human");
            Remove(ground.LocalSeatCount);yield return Capture("21-removed-bot");
            // Native start/repeat uses the exact frozen composition.
            Invoke("Begin",false);yield return new WaitForSecondsRealtime(.5f);Check(ground.Running,"Match did not launch");
            var frozen=JsonUtility.ToJson(ground.Composition.Read());Invoke("Pause","Synthetic roster review");Invoke("Repeat");yield return null;
            Check(JsonUtility.ToJson(ground.Composition.Read())==frozen,"Repeat changed frozen composition");
            Invoke("Menu");Click("setup-step-2");yield return Capture("22-return-from-match");
            InputSystem.RemoveDevice(pad);
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: actual muted native Player pixels; synthetic mouse, keyboard and gamepad navigation; default mouse/keyboard/gamepad one human + one bot, FFA 2–8, teams 4+4/7+1/6+2, add/edit/remove, four views, promotion/demotion, AI-only, disabled/removed/replaced devices, preserved identity, back draft and frozen Repeat. Physical controllers/TV and human visual acceptance pending.");
            Debug.Log("NATIVE_ROSTER_CARDS_REVIEW_COMPLETE "+directory);
        }
    }
}
#endif

#endif
