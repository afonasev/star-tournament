using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ParticipantColorIdentityTests
    {
        Scene scene;ProvingGround ground;
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        void AssertBody(int participant)
        {
            var root=ground.transform.Find("player-"+(participant+1)+"/trooper-presentation");int slots=0,helmets=0,thighs=0;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name!="weapon:joined"&&!r.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter")))
                for(int i=0;i<renderer.sharedMaterials.Length;i++)
                {
                    var mat=renderer.sharedMaterials[i];if(!mat||!TrooperIdentityPresentation.IsBodyZone(mat))continue;
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                    foreach(string property in mat.HasProperty("_IdentityColor")?new[]{"_IdentityColor"}:new[]{"_BaseColor","baseColorFactor"})if(mat.HasProperty(property))Assert.That(block.GetColor(property),Is.EqualTo(ground.Composition.Participant(participant).Color),renderer.name+"/"+mat.name);
                    if(mat.HasProperty("_IdentityColor"))
                    {
                        Assert.That(mat.GetColor("_IdentityColor"),Is.EqualTo(ground.Composition.Participant(participant).Color),"owned material color "+participant+"/"+mat.name);
                        Assert.That(mat.GetFloat("_IdentityMode"),Is.GreaterThan(0),"owned material mode "+mat.name);
                        Assert.That(block.GetVector("_IdentitySurface").w,Is.EqualTo(ground.ParticipantPaletteProfile.Get("participant.surface.panelFloor")));
                        Assert.That(block.GetFloat("_IdentityMode"),Is.GreaterThan(0));
                        var source=ground.TrooperBodyPrefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).First(m=>m&&m.name==mat.name);
                        Assert.That(mat.GetColor("baseColorFactor"),Is.EqualTo(source.GetColor("baseColorFactor")),"source factor must not be multiplied by identity");
                        foreach(var property in new[]{"baseColorTexture","normalTexture","metallicRoughnessTexture"})
                            Assert.That(mat.GetTexture(property),Is.EqualTo(source.GetTexture(property)),"keep authored maps "+property);
                    }
                    if(mat.name=="TECI_helmet")helmets++;if(mat.name=="pants")thighs++;
                    slots++;
                }
            Assert.That(slots,Is.GreaterThan(3));
            Assert.That(helmets,Is.GreaterThan(0));Assert.That(thighs,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator OrdinaryEightAiStartBoostPauseRespawnRepeatAndUiUseFrozenColors()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            B("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            for(int seat=0;seat<4;seat++)ground.SetSeatAi(seat,true);
            for(int p=4;p<8;p++)ground.AddBot();
            var preview=JsonUtility.ToJson(ground.SetupComposition().Read());
            for(int i=0;i<5;i++)Assert.That(JsonUtility.ToJson(ground.SetupComposition().Read()),Is.EqualTo(preview));
            B("Начать — четыре игрока").onClick.Invoke();yield return null;
            Assert.That(ground.Running,Is.True);Assert.That(ground.Composition.Read().Participants.Select(p=>p.Color).Distinct().Count(),Is.EqualTo(8));
            var frozen=JsonUtility.ToJson(ground.Composition.Read());for(int p=0;p<8;p++)AssertBody(p);
            var snapshot=ground.Session.Capture();snapshot.DamageRemaining[0]=5;snapshot.DamageRemaining[1]=5;ground.Session.Restore(snapshot);yield return null;
            AssertBody(0);AssertBody(1);
            foreach(int p in new[]{0,1})
            {
                var body=ground.transform.Find("player-"+(p+1)+"/trooper-presentation");
                foreach(string weapon in new[]{"weapon:joined","automatic-rifle","pulse-launcher","cutter"})
                {
                    var renderer=body.GetComponentsInChildren<Renderer>(true).First(r=>r.name==weapon||r.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name==weapon));
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                    var mat=renderer.sharedMaterials[0];string property=mat.HasProperty("_BaseColor")?"_BaseColor":"baseColorFactor";
                    Assert.That(block.GetColor(property),Is.EqualTo(Color.red),weapon);
                }
            }
            ground.SendMessage("Pause","color test");yield return null;Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));
            B("Продолжить").onClick.Invoke();ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500,0,ground.Session.Life(0).Life);
            for(int i=0;i<300&&ground.Session.Life(1).Dead;i++)ground.Session.Tick(new LocalAction[8],.02f);
            yield return null;AssertBody(1);Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));
            var names=ground.GetComponentsInChildren<Text>(true);
            for(int seat=0;seat<4;seat++)Assert.That(names.Any(t=>t.text==ground.Composition.Participant(seat).Name&&t.color==ground.Composition.Participant(seat).Color),Is.True,"viewport name "+seat);
            // Live/results use the same composition; every name keeps identity even below the score leader.
            var table=new NativeStandingsView(ground.transform,names[0].font,20,"color-table",Vector2.zero,Vector2.one);
            table.Show(true,ground.Session.Match.Read(),false,ground.Composition);
            for(int p=0;p<8;p++)
            {
                var info=ground.Composition.Participant(p);
                var name=table.Root.GetComponentsInChildren<Text>().Single(t=>t.name=="cell-0"&&t.text==info.Name&&t.color==info.Color);
                var row=name.transform.parent;
                Assert.That(info.Name+" · "+row.Find("detail").GetComponent<Text>().text,Is.EqualTo(info.Label),"identity label for seat "+p);
                var expected=info.Kind==NativeParticipantKind.Bot?StandingsIcon.Symbol.Bot:info.Kind==NativeParticipantKind.LocalHuman?StandingsIcon.Symbol.Human:StandingsIcon.Symbol.Fixture;
                Assert.That(row.Find("kind").GetComponent<StandingsIcon>().Kind,Is.EqualTo(expected),"identity icon for seat "+p);
            }
            Object.Destroy(table.Root);
            ground.SendMessage("Pause","repeat");B("Повторить матч").onClick.Invoke();yield return null;
            Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));for(int p=0;p<8;p++)AssertBody(p);

        }
        [UnityTest] public IEnumerator MixedReviewHasOwnedMaterialsForEveryParticipant()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return null;ground.StartParticipantReview(NativeParticipantReview.Mixed(1,false));yield return null;
            for(int p=0;p<8;p++)AssertBody(p);
        }
    }
}
