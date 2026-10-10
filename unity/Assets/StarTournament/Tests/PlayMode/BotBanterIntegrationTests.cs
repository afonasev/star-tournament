using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class BotBanterIntegrationTests
    {
        Scene scene;
        NativeBotBanter observer;
        float previousVolume;bool hadText,hadVoice;int oldText,oldVoice;
        [SetUp] public void Mute()
        {
            previousVolume=AudioListener.volume;AudioListener.volume=0;
            hadText=PlayerPrefs.HasKey(NativeBotReactionPreferences.TextKey);hadVoice=PlayerPrefs.HasKey(NativeBotReactionPreferences.VoiceKey);
            oldText=PlayerPrefs.GetInt(NativeBotReactionPreferences.TextKey);oldVoice=PlayerPrefs.GetInt(NativeBotReactionPreferences.VoiceKey);
            NativeBotReactionPreferences.SetText(true);NativeBotReactionPreferences.SetVoice(false);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {observer?.Dispose();observer=null;if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);AudioListener.volume=previousVolume;
            if(hadText)PlayerPrefs.SetInt(NativeBotReactionPreferences.TextKey,oldText);else PlayerPrefs.DeleteKey(NativeBotReactionPreferences.TextKey);
            if(hadVoice)PlayerPrefs.SetInt(NativeBotReactionPreferences.VoiceKey,oldVoice);else PlayerPrefs.DeleteKey(NativeBotReactionPreferences.VoiceKey);PlayerPrefs.Save();}
        static NativeMatchComposition Composition(int locals=1)
        {
            var roster=NativeMatchRoster.Ffa(locals+1);
            var info=Enumerable.Range(0,locals+1).Select(p=>new NativeParticipantInfo(p==0?NativeParticipantKind.Bot:NativeParticipantKind.LocalHuman,
                p==0?"Бот Борис":"Игрок "+p,NativeStandingsView.Identity(roster.Read(),p,false),p==0?1:-1)).ToArray();
            return new NativeMatchComposition(roster,info,Enumerable.Range(1,locals).ToArray());
        }
        static BotBanterPolicy Always()=>new BotBanterPolicy { InitialSilence=0,GlobalMinimum=0,GlobalMaximum=0,BotMinimum=0,BotMaximum=0,Chance=1 };
        static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static void Call(object owner,string name)=>owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
        [UnityTest] public IEnumerator ActualAttackDamageOrderingAndSnapshotRemainIndependent()
        {
            scene=SceneManager.CreateScene("bot-banter-events",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var root=new GameObject("events");SceneManager.MoveGameObjectToScene(root,scene);
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("shot.spread",0);combat.Set("rifle.spread",0);combat.Set("shot.damage",100);
            var arena=root.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            var motors=new CharacterMotor[2];
            for(int p=0;p<2;p++){var go=new GameObject("p"+p);go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;motors[p]=go.AddComponent<CharacterMotor>();motors[p].Initialize(move,new Vector3(0,0,p==0?-3:3));}
            var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),combat);
            EquippedCombatFixture.Equip(session);
            var input=new LocalAction[2];input[0].SelectWeapon=WeaponSelection.Shotgun;session.Tick(input,.02f);
            for(int i=0;i<52;i++)session.Tick(new LocalAction[2],.02f);
            // Exact fixture poses used by existing combat tests; no movement during the accepted shot.
            motors[0].RestoreState(new ParticipantState{Position=new Vector3(0,0,-3),Yaw=0,Grounded=true});
            motors[1].RestoreState(new ParticipantState{Position=new Vector3(0,0,3),Yaw=180,Grounded=true});Physics.SyncTransforms();
            var baseline=session.Capture();var order=new List<string>();
            session.AttackEmitted+=(p,l,w,t,o,d,r)=>order.Add("attack");
            session.Damaged+=n=>{order.Add("damage");Assert.That(n.Shooter,Is.EqualTo(0));Assert.That(n.ShooterLife,Is.EqualTo(baseline.Lives[0].Life));Assert.That(n.HealthLost+n.ArmorLost,Is.GreaterThan(0));};
            session.Died+=n=>order.Add("death");session.ShotResolved+=n=>order.Add("resolved");session.TickCompleted+=()=>order.Add("completed");
            observer=new NativeBotBanter(session,Composition(),scene.GetPhysicsScene(),move.Get("camera.eyeHeight"),100,4,Always());
            input=new LocalAction[2];input[0].Fire=true;
            session.Tick(input,.02f);
            Assert.That(session.Life(1).Dead,Is.True,"Real shotgun must kill fixture");
            CollectionAssert.AreEqual(new[]{"attack","damage","death","resolved","completed"},order);
            Assert.That(observer.Banter.Current.HasValue,Is.True);Assert.That(observer.Banter.Current.Value.Reason,Is.EqualTo(BotBanterReason.NoReply));
            string observed=JsonUtility.ToJson(session.Capture());observer.Dispose();observer=null;
            session.Restore(baseline);session.Tick(input,.02f);Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(observed),"Presentation cannot change exact simulation snapshot");
            yield return null;
        }
        [UnityTest] public IEnumerator HudOneToFourViewsPauseRestoreRepeatAndLayout()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            ground.GetType().GetField("FullHealReviewManualTick",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,true);
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BANTER_EVIDENCE");if(!string.IsNullOrEmpty(output))Directory.CreateDirectory(output);
            for(int locals=1;locals<=4;locals++)
            {
                ground.StartBotReview(Composition(locals),7);yield return NativeLoadingTestScene.Wait(ground);yield return null;
                var comp=ground.Composition;
                ((NativeBotBanter)Field(ground,"botBanter")).Dispose();
                observer=new NativeBotBanter(ground.Session,comp,scene.GetPhysicsScene(),ground.Profile.Get("camera.eyeHeight"),100,3,Always());
                ground.GetType().GetField("botBanter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,observer);
                ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,100,0,ground.Session.Life(0).Life);
                observer.Flush();
                // Real deaths/respawns establish the three-kill history, without driving the AI.
                for(int i=0;i<2;i++)
                {
                    int ticks=0;
                    while(ground.Session.Life(1).Dead)
                    {
                        Assert.That(++ticks,Is.LessThan(400),"Fixture respawn must finish");
                        ground.Session.Tick(new LocalAction[comp.ParticipantCount],.02f);
                    }
                    ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,100,0,ground.Session.Life(0).Life);
                    observer.Flush();
                }
                yield return null;
                var labels=ground.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("bot-banter-notice-")&&t.gameObject.activeInHierarchy).ToArray();
                Assert.That(labels.Length,Is.EqualTo(locals));Assert.That(labels[0].text,Does.Contain("Бот Борис"));
                foreach(var label in labels)
                {
                    Assert.That(label.text,Is.EqualTo(labels[0].text));Assert.That(label.raycastTarget,Is.False);
                    Assert.That(label.rectTransform.anchorMax.y,Is.LessThan(.3));
                    Assert.That(label.rectTransform.anchorMin.y,Is.GreaterThan(.1));
                }
                if(!string.IsNullOrEmpty(output)&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                    yield return EditorUiCapture.Capture(ground,Path.Combine(output,"bot-banter-"+locals+"-views.png"));
                double time=ground.Session.Time;Call(ground,"FullHealReviewPause");yield return null;
                Assert.That(ground.Session.Time,Is.EqualTo(time));foreach(var label in labels)Assert.That(label.text,Is.Empty);
                Call(ground,"FullHealReviewResume");yield return null;Assert.That(labels[0].text,Is.Not.Empty);
                ground.Session.Restore(ground.Session.Capture());yield return null;foreach(var label in labels)Assert.That(label.text,Is.Empty);
                Assert.That(observer.Banter.Current,Is.Null);
                Call(ground,"FullHealReviewRepeat");yield return NativeLoadingTestScene.Wait(ground);yield return null;
                Assert.That(((NativeBotBanter)Field(ground,"botBanter")).Banter.Current,Is.Null);
                observer=null;
            }
        }
    }
}
