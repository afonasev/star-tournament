using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class DamageBonusAlertTests
    {
        Scene scene;Gamepad[] pads;
        [UnityTest] public IEnumerator GlobalAlertsFollowLifecycleOnceAndRestoreDoesNotReplay()
        {
            const string key="StarTournament.Audio.EffectsPercent";
            bool had=PlayerPrefs.HasKey(key);int prior=PlayerPrefs.GetInt(key);float listener=AudioListener.volume;
            AudioListener.volume=0;NativeAudioPreferences.SetEffects(80);
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
                ground.StartCombatReview(pads);typeof(ProvingGround).GetField("FullHealReviewManualTick",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,true);
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var source=ground.transform.Find("native-game-audio/damage-bonus-alert").GetComponent<AudioSource>();
                int spawns=0;ground.Session.DamageBonusAppeared+=()=>spawns++;
                var snap=ground.Session.Capture();Assert.That(ground.Session.HasDamagePickup,Is.True);
                snap.DamagePickup.Available=false;snap.DamagePickup.Remaining=.01;
                ground.Session.Restore(snap);
                var anchor=ground.Session.DamagePickup.Anchor;
                for(int p=0;p<4;p++)ground.PlaceCombatReviewSeat(p,anchor+Vector3.forward*(5+p*2),0);
                ground.Session.Tick(new LocalAction[4],.02f);yield return null;
                Assert.That(spawns,Is.EqualTo(1));Assert.That(source.clip.name,Is.EqualTo("damage-bonus-spawn"));
                var labels=ground.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("damage-bonus-notice-")).ToArray();
                Assert.That(labels.Length,Is.EqualTo(4));
                foreach(var label in labels){Assert.That(label.text,Is.EqualTo("ПОЯВИЛСЯ БОНУС УРОНА"));Assert.That(label.color,Is.EqualTo(Color.red));Assert.That(label.raycastTarget,Is.False);Assert.That(label.rectTransform.anchorMin.y,Is.GreaterThan(.5f));}
                ground.Session.Tick(new LocalAction[4],.02f);Assert.That(spawns,Is.EqualTo(1));
                source.Stop();source.clip=null;
                ground.PlaceCombatReviewSeat(3,anchor,0);ground.Session.Tick(new LocalAction[4],.02f);yield return null;
                Assert.That(ground.Session.DamageBoostRemaining(3),Is.GreaterThan(0));
                Assert.That(source.clip.name,Is.EqualTo("damage-bonus-pickup"));Assert.That(source.volume,Is.GreaterThan(0),"Global alert cannot attenuate by remote distance");
                foreach(var label in labels)Assert.That(label.text,Is.EqualTo("БОНУС УРОНА ПОДОБРАН"));
                var voices=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>();
                Assert.That(voices.Count(s=>s.clip&&s.clip.name=="damage-bonus-pickup"),Is.EqualTo(1));
                Assert.That(voices.Any(s=>s.isPlaying&&s.clip&&s.clip.name=="pickup"),Is.False,"No generic pickup duplication");
                typeof(ProvingGround).GetMethod("FullHealReviewPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null);yield return null;Assert.That(source.isPlaying,Is.False);foreach(var label in labels)Assert.That(label.text,Is.Empty);
                typeof(ProvingGround).GetMethod("FullHealReviewResume",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null);yield return null;foreach(var label in labels)Assert.That(label.text,Is.EqualTo("БОНУС УРОНА ПОДОБРАН"));Assert.That(source.isPlaying,Is.False);
                for(int i=0;i<160;i++)ground.Session.Tick(new LocalAction[4],.02f);yield return null;
                foreach(var label in labels)Assert.That(label.text,Is.Empty);
                var cooled=ground.Session.Capture();cooled.DamagePickup.Remaining=.01;ground.Session.Restore(cooled);source.clip=null;
                Assert.That(source.isPlaying,Is.False);Assert.That(spawns,Is.EqualTo(1));
                ground.Session.Tick(new LocalAction[4],.02f);yield return null;Assert.That(spawns,Is.EqualTo(2));
                Assert.That(source.clip.name,Is.EqualTo("damage-bonus-spawn"));
                var available=ground.Session.Capture();ground.Session.Restore(available);yield return null;
                Assert.That(source.isPlaying,Is.False);foreach(var label in labels)Assert.That(label.text,Is.Empty);
                ground.Session.Tick(new LocalAction[4],.02f);Assert.That(spawns,Is.EqualTo(2),"Restore must not fabricate a spawn");
                NativeAudioPreferences.SetEffects(0);source.clip=null;
                ground.PlaceCombatReviewSeat(3,anchor+Vector3.forward*5,0);ground.Session.Tick(new LocalAction[4],.02f);
                ground.PlaceCombatReviewSeat(3,anchor,0);ground.Session.Tick(new LocalAction[4],.02f);yield return null;
                Assert.That(source.isPlaying,Is.False);Assert.That(source.clip,Is.Null);foreach(var label in labels)Assert.That(label.text,Is.EqualTo("БОНУС УРОНА ПОДОБРАН"));
                ground.Profile.Set("ui.damageBonusFontSize",40);ground.Profile.Set("ui.damageBonusTopInset",.2f);ground.Profile.Set("ui.damageBonusHeight",.15f);
                ground.StartCombatReview(pads,backgroundDiagnostic:true);yield return null;
                foreach(var label in labels)
                {
                    Assert.That(label.fontSize,Is.EqualTo(40),"Next match must apply selected Lab notice size");
                    Assert.That(label.rectTransform.anchorMin.y,Is.EqualTo(.65f).Within(.0001f));
                    Assert.That(label.rectTransform.anchorMax.y,Is.EqualTo(.8f).Within(.0001f));
                    Assert.That(label.text,Is.Empty,"New match clears the previous notice");
                }
            }
            finally
            {
                AudioListener.volume=listener;if(had)PlayerPrefs.SetInt(key,prior);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
