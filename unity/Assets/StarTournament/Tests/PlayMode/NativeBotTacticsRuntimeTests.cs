using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public class NativeBotTacticsRuntimeTests
    {
        Scene scene;ProvingGround ground;
        [UnityTearDown] public IEnumerator Cleanup(){if(ground)ground.enabled=true;if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator NativeActionsActuallyUnlockWeaponsCollectBonusesAndRestoreWithoutInstantKnowledge()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            ground.StartBotReview(NativeBotTacticsReview.Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Normal,0),271001);ground.enabled=false;
            var session=ground.Session;var driver=ground.BotDriver;float dt=1/ground.Profile.Get("simulation.fixedTickHz");var actions=new LocalAction[4];int weapons=0,bonuses=0,damage=0;
            session.PickupCollected+=(p,id,kind)=>{if(kind==NativeBotPickupKind.Weapon)weapons++;else bonuses++;};session.Fired+=(p,d)=>{if(d>0)damage++;};
            driver.ProduceActions(actions,dt);for(int p=0;p<4;p++)Assert.That(driver.Pickups.Read(p),Is.Empty,"Bootstrap obeys delay");session.Tick(actions,dt);
            var saved=driver.Capture();var corrupt=driver.Capture();corrupt.Pickups.Pending[0].DeliverAt=0;string before=JsonUtility.ToJson(driver.Capture());Assert.Throws<ArgumentException>(()=>driver.Restore(corrupt));Assert.That(JsonUtility.ToJson(driver.Capture()),Is.EqualTo(before));driver.Restore(saved);
            int ticks=0;while(session.Time<90&&session.Match.Phase!=NativeMatchPhase.Finished)
            {Array.Clear(actions,0,4);driver.ProduceActions(actions,dt);session.Tick(actions,dt);if(++ticks%500==0)yield return null;}
            Assert.That(weapons,Is.GreaterThan(0),"Actual physical weapon collection required");Assert.That(bonuses,Is.GreaterThan(0),"Actual useful bonus collection required");Assert.That(damage,Is.GreaterThan(0),"Actual combat outcomes required");
            Assert.That(Enumerable.Range(0,4).Sum(p=>driver.Planner(p).UnlockDecisions),Is.GreaterThan(0),"Purposeful weapon objectives required");
            UnityEngine.Debug.Log($"BOT_TACTICS_NATIVE_OUTCOMES weapons={weapons} bonuses={bonuses} damagingActions={damage}");
        }
    }
}
