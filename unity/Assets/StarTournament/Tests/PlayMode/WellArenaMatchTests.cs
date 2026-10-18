using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WellArenaMatchTests
    {
        Scene scene;
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}

        [UnityTest] public IEnumerator RealMatchPauseDriverRestoreAndRepeatResetLiftState()
        {
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            try
            {
                yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(WellArenaCatalog.Id);
                var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(8),Enumerable.Range(0,8).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Well bot "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                ground.StartBotReview(composition,7219);yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));
                ground.OpenPauseReviewSeat(0);var arena=ground.CameraReviewArena;var lifts=arena.Lifts.Capture();var south=lifts.Single(x=>x.Id=="south");south.Height=WellArenaCatalog.Storey;south.Target=2*WellArenaCatalog.Storey;south.Phase=WellLiftPhase.Moving;south.Countdown=0;south.IdleSeconds=0;arena.Lifts.Restore(lifts);
                lifts=arena.Lifts.Capture();lifts.Single(x=>x.Id=="north").IdleSeconds=7.5;arena.Lifts.Restore(lifts);
                var saved=ground.Session.Capture();var bots=ground.BotDriver.Capture();Assert.That(bots.Lifts.Length,Is.EqualTo(8));
                // Restore owns the decoded input, not rounding performed by Unity's JSON double parser.
                // Keep byte equality against that exact supplied input, including every existing match statistic.
                var decoded=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(saved));var decodedBots=JsonUtility.FromJson<NativeBotDriverSnapshot>(JsonUtility.ToJson(bots));
                ground.Session.Restore(decoded);ground.BotDriver.Restore(decodedBots);Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(JsonUtility.ToJson(decoded)));Assert.That(JsonUtility.ToJson(ground.BotDriver.Capture()),Is.EqualTo(JsonUtility.ToJson(decodedBots)));
                yield return new WaitForSecondsRealtime(.2f);Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(JsonUtility.ToJson(decoded)),"paused world does not advance");
                ground.SendMessage("Repeat");yield return NativeLoadingTestScene.Wait(ground);
                Assert.That(ground.CameraReviewArena.Lifts.Capture().Single(x=>x.Id=="north").Height,Is.EqualTo(2*WellArenaCatalog.Storey));Assert.That(ground.CameraReviewArena.Lifts.Capture().Single(x=>x.Id=="south").Height,Is.EqualTo(0));Assert.That(ground.Session.Time,Is.LessThan(1));Assert.That(ground.CameraReviewArena.Lifts.Capture().All(l=>l.IdleSeconds<1),Is.True,"Repeat resets accumulated idle time");
            }
            finally
            {
                if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            }
        }
    }
}
