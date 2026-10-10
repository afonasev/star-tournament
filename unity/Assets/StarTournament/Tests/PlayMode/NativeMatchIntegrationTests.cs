using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeMatchIntegrationTests
    {
        Scene scene;string labDirectory,oldLabEnvironment;
        [SetUp]public void IsolateDesignHistory()
        {
            labDirectory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"st-match-lab-"+Guid.NewGuid());System.IO.Directory.CreateDirectory(labDirectory);
            oldLabEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",System.IO.Path.Combine(labDirectory,"history.json"));
        }
        readonly Gamepad[] pads=new Gamepad[4];
        ProvingGround ground;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var p in pads) if(p!=null && p.added)InputSystem.RemoveDevice(p);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",oldLabEnvironment);if(System.IO.Directory.Exists(labDirectory))System.IO.Directory.Delete(labDirectory,true);
        }
        [UnityTest] public IEnumerator SnapshotAllowsMutualLethalAndLateSeatForcesOvertime()
        {
            scene=SceneManager.CreateScene("match-atomic-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("owner");SceneManager.MoveGameObjectToScene(owner,scene);
            var move=ProvingProfile.CreateDefault();var life=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("shot.spread",0);combat.Set("shot.damage",100);
            var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            var motors=new CharacterMotor[4];
            for(int i=0;i<4;i++){var go=new GameObject("seat");go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(i*4,0,0));}
            motors[1].Initialize(move,new Vector3(0,0,4));motors[1].Tick(new LocalAction{LookDegrees=new Vector2(180,0)},.02f);
            var profile=ProvingProfile.CreateLegacyMatchDefault();for(int i=1;i<=4;i++)profile.Set("score.chainTotal"+i,1000);
            var config=NativeMatchConfiguration.Default(profile);config.DurationMinutes=1;config.TargetEnabled=true;config.TargetPoints=1000;
            var match=new NativeMatchState(4,config,profile,50);
            for(int i=0;i<2999;i++){match.BeginTick();match.EndTick();}
            var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);
            EquippedCombatFixture.Equip(session);
            var equipped=session.Capture();
            for(int i=0;i<2;i++){equipped.Lives[i].SelectedWeapon=WeaponId.Shotgun;equipped.Lives[i].Ammo=equipped.Lives[i].ShotgunAmmo;}
            session.Restore(equipped); // Set up the scoring order directly; switching timing is covered separately.
            yield return null;
            var actions=new LocalAction[4];actions[0].Fire=actions[1].Fire=true;actions[0].SelectWeapon=actions[1].SelectWeapon=WeaponSelection.Shotgun;session.Tick(actions,.02f);
            Assert.That(session.Life(0).Dead && session.Life(1).Dead,Is.True,"Both admitted shots survive the first death");
            Assert.That(match.Phase,Is.EqualTo(NativeMatchPhase.Overtime));Assert.That(match.Read().Trigger,Is.EqualTo("score-limit"));
            Assert.That(match.Read().Standings.Take(2).All(r=>r.Kills==1 && r.Deaths==1 && r.Score==1000),Is.True);
            session.ApplyDamage(2,1,200,0,1);session.Tick(new LocalAction[4],.02f);
            Assert.That(match.Phase,Is.EqualTo(NativeMatchPhase.Finished));
            double clock=session.Time;var frozen=JsonUtility.ToJson(match.Read());
            Assert.That(session.ApplyDamage(3,1,100,1,1).Applied,Is.Zero);session.Tick(actions,.02f);
            Assert.That(session.Time,Is.EqualTo(clock));Assert.That(JsonUtility.ToJson(match.Read()),Is.EqualTo(frozen));
        }
        IEnumerator Load(bool teams=false)
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            if(teams){Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);ground.SetMatchMode(NativeMatchMode.Teams);ground.SetTeam(0,NativeTeam.TeamA);ground.SetTeam(1,NativeTeam.TeamA);ground.SetTeam(2,NativeTeam.TeamB);ground.SetTeam(3,NativeTeam.TeamB);}
            ground.StartCombatReview(pads);yield return new WaitForFixedUpdate();
        }
        Button Button(string text) => ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==text);
        void ChooseGuestsForAssignedSeats()
        {
            var seats=(SeatInputCoordinator)typeof(ProvingGround).GetField("input",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
            for(int seat=0;seat<ground.LocalSeatCount;seat++)Assert.That(seats.Assign(seat,pads[seat]),Is.True);
            Button("setup-next").onClick.Invoke();Button("setup-next").onClick.Invoke();
            for(int seat=0;seat<ground.LocalSeatCount;seat++)
            {
                Button("roster-card-"+seat).onClick.Invoke();Button("roster-identity").onClick.Invoke();
                ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="roster-choice-guest"&&b.gameObject.activeInHierarchy).onClick.Invoke();Button("roster-done").onClick.Invoke();
            }
        }
        Text Notice(int seat) => ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="kill-notice-"+seat);
        [UnityTest] public IEnumerator SelfKillUsesNormalColorWithRedPenaltyAndDoesNotNameSelfAsKiller()
        {
            yield return Load();var session=ground.Session;
            session.ApplyDamage(0,session.Life(0).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).text,Does.StartWith("Вы убили себя · <color=#FF0000>−200</color>\nВозрождение через "));
            Assert.That(Notice(0).text,Does.Not.Contain("Вас убил"));
            Assert.That(Notice(0).color,Is.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator TeamKillIsRedWithoutEnemyScoreOrStreak()
        {
            yield return Load(true);var session=ground.Session;
            session.ApplyDamage(1,session.Life(1).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).text,Is.EqualTo("Вы убили союзника "+ground.Composition.Participant(1).Name.Replace('<','‹').Replace('>','›')+" · −200"));
            Assert.That(Notice(0).color,Is.EqualTo(Color.red));
            Assert.That(Notice(1).text,Does.StartWith("Вас убил "));
            Assert.That(Notice(1).color,Is.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator EnemyKillAfterTeamKillRestoresNormalColorAndPreservesNameColor()
        {
            yield return Load(true);var session=ground.Session;
            session.ApplyDamage(1,session.Life(1).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).color,Is.EqualTo(Color.red));
            session.ApplyDamage(2,session.Life(2).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).text,Does.StartWith("Вы убили <color=#"+ColorUtility.ToHtmlStringRGB(ground.Composition.Participant(2).Color)+">"));
            Assert.That(Notice(0).color,Is.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator SelfKillAfterTeamKillShowsRedPenaltyInNormalText()
        {
            yield return Load(true);var session=ground.Session;
            session.ApplyDamage(1,session.Life(1).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).color,Is.EqualTo(Color.red));
            session.ApplyDamage(0,session.Life(0).Life,10000,0,session.Life(0).Life);
            yield return null;yield return null;
            Assert.That(Notice(0).text,Does.StartWith("Вы убили себя · <color=#FF0000>−200</color>\nВозрождение через "));
            Assert.That(Notice(0).color,Is.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator TimedSeriesShowsEventPointsGoldAndRoutesOnlyThirdAndFourthVoices()
        {
            float listener=AudioListener.volume;AudioListener.volume=0;
            try
            {
                yield return Load();var session=ground.Session;
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var voice=ground.transform.Find("native-game-audio/kill-series-voice").GetComponent<AudioSource>();
                var before=session.Capture();var notices=new System.Collections.Generic.List<DeathNotice>();session.Died+=notices.Add;
                session.ApplyDamage(1,session.Life(1).Life,10000,0,session.Life(0).Life);yield return null;
                Assert.That(Notice(0).text,Does.Contain("+100"));Assert.That(Notice(0).text,Does.Not.Contain("Серия убийств!"));Assert.That(Notice(0).color,Is.EqualTo(Color.white));Assert.That(voice.clip,Is.Null);
                session.ApplyDamage(2,session.Life(2).Life,10000,0,session.Life(0).Life);yield return null;
                Assert.That(Notice(0).text,Does.Contain("+200"));Assert.That(Notice(0).text,Does.Contain("Серия убийств! · 2"));Assert.That(Notice(0).color,Is.EqualTo(NativeKillNotice.SeriesColor));Assert.That(voice.clip,Is.Null);
                session.ApplyDamage(3,session.Life(3).Life,10000,0,session.Life(0).Life);yield return null;
                Assert.That(Notice(0).text,Does.Contain("+300"));Assert.That(voice.clip.name,Is.EqualTo("kill-triple"));Assert.That(voice.spatialBlend,Is.Zero);
                Assert.That(notices.Last().Score.Points,Is.EqualTo(300));
                if(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UI_EVIDENCE") is string directory)
                {
                    yield return CaptureSeriesHud(directory);
                }
                var current=session.Capture();current.Lives[1]=before.Lives[1];current.Poses[1]=before.Poses[1];session.Restore(current);
                Assert.That(voice.isPlaying,Is.False);session.ApplyDamage(1,session.Life(1).Life,10000,0,session.Life(0).Life);yield return null;
                Assert.That(Notice(0).text,Does.Contain("+400"));Assert.That(Notice(0).text,Does.Contain("Серия убийств! · 4"));Assert.That(voice.clip.name,Is.EqualTo("kill-quadruple"));
                audio.SetDiagnosticMute(true);Assert.That(voice.isPlaying,Is.False);Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).Score,Is.EqualTo(1000));
            }
            finally{AudioListener.volume=listener;}
        }
        IEnumerator CaptureSeriesHud(string directory)
        {
            var canvas=Notice(0).canvas.rootCanvas;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var go=new GameObject("series-evidence-camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=540;camera.aspect=1920f/1080;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(8,15,27,255);
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            var prior=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
                foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())graphic.Rebuild(CanvasUpdate.PreRender);
                if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)
                {var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target};Assert.That(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request),Is.True);UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);}
                else camera.Render();
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                System.IO.Directory.CreateDirectory(directory);System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"series-four-views.png"),image.EncodeToPNG());
            }
            finally
            {canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;RenderTexture.active=prior;camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator KillcamViewPauseRepeatAndMenuKeepOneSceneAndFreshLifecycle()
        {
            yield return Load();var old=ground.Session;
            old.ApplyDamage(1,old.Life(1).Life,100,0,1);
            InputSystem.QueueStateEvent(pads[1],new GamepadState().WithButton(GamepadButton.Select));
            yield return null;yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.transform.Find("native-ui/standings-1").gameObject.activeSelf,Is.True);
            Assert.That(ground.transform.Find("native-ui/standings-0").gameObject.activeSelf,Is.False);
            InputSystem.QueueStateEvent(pads[1],new GamepadState());
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;
            Assert.That(ground.Running,Is.False);long tick=old.Match.Read().Tick;double clock=old.Time;
            yield return new WaitForSecondsRealtime(.1f);Assert.That(old.Time,Is.EqualTo(clock));Assert.That(old.Match.Read().Tick,Is.EqualTo(tick));
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return null;
            for(int n=0;n<3;n++)
            {
                Button("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);yield return new WaitForFixedUpdate();
                yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.Session,Is.Not.SameAs(old));Assert.That(ground.Session.ShotCount,Is.Zero);
                Assert.That(old.ApplyDamage(0,1,100).Applied,Is.Zero);
                Assert.That(ground.Session.Match.Read().Standings.All(r=>r.Score==0 && r.Deaths==0),Is.True);
                for(int i=0;i<4;i++){Assert.That(ground.Session.Life(i).Life,Is.EqualTo(1));Assert.That(ground.Session.Life(i).Health,Is.EqualTo(100));}
                Assert.That(ground.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("corpse-")),Is.False);
                Assert.That(ground.GetComponentsInChildren<EventSystem>(true).Length,Is.EqualTo(1));
                Assert.That(ground.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
                Assert.That(ground.GetComponentsInChildren<ProvingArena>(true).Length,Is.EqualTo(1));
            }
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return null;yield return new WaitForFixedUpdate();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(1));
            var last=ground.Session;Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);ChooseGuestsForAssignedSeats();yield return null;
            Assert.That(ground.Running,Is.False);clock=last.Time;yield return new WaitForFixedUpdate();Assert.That(last.Time,Is.EqualTo(clock));
            Button("Начать — четыре игрока").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.Session,Is.Not.SameAs(last));
        }
        [UnityTest] public IEnumerator DisconnectSelectsActiveMenuAndReconnectCanNavigateToResume()
        {
            yield return Load();InputSystem.RemoveDevice(pads[3]);yield return null;yield return null;
            Assert.That(ground.Running,Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Button("В главное меню").gameObject));
            Assert.That(EventSystem.current.currentSelectedGameObject.activeInHierarchy,Is.True);
            InputSystem.AddDevice(pads[3]);yield return null;yield return null;
            Assert.That(Button("Продолжить").interactable,Is.True);
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.DpadUp));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.DpadUp));yield return null;yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Button("fallback-settings").gameObject));
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.DpadUp));yield return null;yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Button("Продолжить").gameObject));
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
        }
        [UnityTest] public IEnumerator ResultsRepeatPreservesFrozenSettingsAndSetupCanChangeNextMatch()
        {
            yield return Load();var old=ground.Session;
            old.ApplyDamage(1,1,100,0,1); // explicit gameplay fixture
            for(int i=0;i<15000;i++){old.Match.BeginTick();old.Match.EndTick();if(old.Match.Phase==NativeMatchPhase.Finished)break;}
            yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.Running,Is.False);Assert.That(ground.transform.Find("native-ui/setup-pause/results-frame/results-table").gameObject.activeSelf,Is.True);
            ground.MatchProfile.Set("score.chainTotal1",200);
            Button("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            ground.Session.ApplyDamage(1,1,100,0,1);
            Assert.That(ground.Session.Match.Read().Standings[0].Score,Is.EqualTo(100));
            Button("В главное меню").onClick.Invoke();Button("main-action-2").onClick.Invoke();yield return null;
            Button("lab-create").onClick.Invoke();yield return null;
            ground.GetComponentsInChildren<InputField>().Single(f=>f.name=="lab-profile-name").text="Match integration copy";Button("submit").onClick.Invoke();yield return null;
            ground.GetComponentsInChildren<InputField>().Single(f=>f.name=="lab-search").text="score.chainTotal1";yield return null;
            ground.GetComponentsInChildren<InputField>().Single(f=>f.name=="input-score.chainTotal1").text="200";Button("lab-save").onClick.Invoke();yield return null;
            var saved=(DesignLabHistory)typeof(ProvingGround).GetField("labHistory",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
            Assert.That(saved.Selected.Snapshot.Get("score.chainTotal1"),Is.EqualTo(200),"Lab save: "+ground.GetComponentsInChildren<Text>().First(t=>t.name=="lab-status").text);
            Button("lab-back").onClick.Invoke();Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);ChooseGuestsForAssignedSeats();yield return null;
            int durationBefore=ground.Configuration.DurationMinutes;
            Button("duration-plus").onClick.Invoke();Assert.That(ground.Configuration.TargetEnabled,Is.True);
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(durationBefore+1));
            Button("Начать — четыре игрока").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Session.Match.Configuration.DurationMinutes,Is.EqualTo(durationBefore+1));Assert.That(ground.Session.Match.Configuration.TargetEnabled,Is.True);
            ground.Session.ApplyDamage(1,1,100,0,1);Assert.That(ground.Session.Match.Read().Standings[0].Score,Is.EqualTo(200));
        }
    }
}
