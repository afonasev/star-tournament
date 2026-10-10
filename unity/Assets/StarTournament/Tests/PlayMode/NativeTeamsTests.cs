using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeTeamsTests
    {
        Scene scene;
        ProvingGround ground;
        [UnityTearDown] public IEnumerator Cleanup() { if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene); }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        NativeMatchRoster Roster(params NativeTeam[] teams)=>new NativeMatchRoster(NativeMatchMode.Teams,teams);
        GameObject Owner()
        {
            scene=SceneManager.CreateScene("teams-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("owner");SceneManager.MoveGameObjectToScene(owner,scene);return owner;
        }
        [UnityTest] public IEnumerator InitialAllocationCoversEveryNonemptyAssignmentAndOccupancy()
        {
            var owner=Owner();var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var arena=owner.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(move),move);yield return null;
            var selector=new SafeSpawnSelector(scene.GetPhysicsScene(),arena,move,combat);
            float separation=ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation");
            for(int count=2;count<=4;count++) for(int mask=1;mask<(1<<count)-1;mask++)
            {
                var roster=Roster(Enumerable.Range(0,count).Select(i=>(mask&(1<<i))!=0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray());
                Assert.That(selector.TryInitial(roster,separation,out var positions),Is.True,$"count={count},mask={mask}");
                Assert.That(positions.Distinct().Count(),Is.EqualTo(count));
                for(int i=0;i<count;i++)for(int j=0;j<i;j++)
                    if(!roster.AreAllies(i,j))Assert.That(Vector3.Distance(positions[i],positions[j]),Is.GreaterThanOrEqualTo(separation));
                foreach(var point in positions) Assert.That(selector.Valid(point,out _),Is.True);
            }
            Assert.That(selector.TryInitial(Roster(NativeTeam.TeamA,NativeTeam.TeamB),float.MaxValue,out var failed),Is.False);Assert.That(failed,Is.Empty);
            Assert.That(selector.TryInitial(Roster(NativeTeam.TeamA,NativeTeam.TeamB),separation,out var initial),Is.True);
            var go=new GameObject("ally-occupancy");go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;
            go.AddComponent<CharacterMotor>().Initialize(move,initial[0]);Physics.SyncTransforms();
            Assert.That(selector.Valid(initial[0],out _),Is.False,"Allied capsule still occupies the slot");
        }
        [UnityTest] public IEnumerator AlliedBodyTakesReducedShotAndBlocksEnemyBehind()
        {
            var owner=Owner();var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("shot.spread",0);
            var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);var motors=new CharacterMotor[4];
            for(int i=0;i<4;i++) { var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(0,0,i*2)); }
            var p=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(p);
            var match=new NativeMatchState(Roster(NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB),config,p,50);
            var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),combat,match);
            yield return null;var action=new LocalAction[4];action[0].Fire=true;int ammo=session.Life(0).Ammo;
            session.Tick(action,.02f);session.Tick(new LocalAction[4],.02f);
            Assert.That(session.Life(0).Ammo,Is.EqualTo(ammo-1));Assert.That(session.Life(1).Health,Is.EqualTo(95));Assert.That(session.Life(2).Health,Is.EqualTo(100));
            Assert.That(session.ApplyDamage(1,1,500,0,1).Applied,Is.EqualTo(95));Assert.That(match.Read().Standings.Sum(r=>r.DamageDealt),Is.Zero);
            motors[1].Initialize(move,new Vector3(8,0,0));session.Tick(new LocalAction[4],1);session.Tick(action,.02f);session.Tick(new LocalAction[4],.02f);session.Tick(new LocalAction[4],.02f);
            Assert.That(session.Life(2).Health,Is.LessThan(100),"Opponent behind moved ally is hittable");
            var ffa=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),combat,new NativeMatchState(4,config,p,50));
            Assert.That(ffa.ApplyDamage(1,1,10,0,1).Applied,Is.EqualTo(10));
        }
        [UnityTest] public IEnumerator RespawnCountsVisibleAlliesAndReservesEveryCapsule()
        {
            var owner=Owner();var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var life=ProvingProfile.CreateCombatDefault();
            var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);var motors=new CharacterMotor[4];
            Vector3[] positions={new Vector3(8,0,-10),new Vector3(-10,0,-10),new Vector3(8,0,8),new Vector3(4,0,8)};
            for(int i=0;i<4;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,positions[i]);}
            var p=ProvingProfile.CreateMatchDefault();var match=new NativeMatchState(Roster(NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA),NativeMatchConfiguration.Default(p),p,50);
            var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);
            yield return null;session.ApplyDamage(0,1,500);session.ApplyDamage(2,1,500);session.ApplyDamage(3,1,500);Physics.SyncTransforms();
            Assert.That(session.Spawns.TryChoose(Array.Empty<CombatTarget>(),out var expected),Is.True);
            Assert.That(session.Spawns.TryChoose(session.LiveTargets(),out var countingAlly),Is.True);
            Assert.That(Vector3.Distance(expected,countingAlly),Is.GreaterThan(.1f),"Fixture must distinguish enemy and ally ranking");
            Assert.That(session.Spawns.Valid(session.Pose(1).Position,out _),Is.False);
            for(int i=0;i<300 && session.Life(0).Dead;i++)session.Tick(new LocalAction[4],.02f);
            Assert.That(session.Life(0).Dead,Is.False);Assert.That(Vector3.Distance(session.Pose(0).Position,countingAlly),Is.LessThan(.1f));
            Assert.That(session.Pose(0).Position,Is.Not.EqualTo(session.Pose(3).Position),"Simultaneous allied respawn reserves distinct slots");
            Assert.That(session.Life(3).Dead,Is.False);
        }
        [UnityTest] public IEnumerator SetupRepeatResultAndFfaRestoreKeepRosterAndIdentityCoherent()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            ground.TeamProfile.Set("spawn.initialOpponentSeparation",5.1f);
            Assert.DoesNotThrow(()=>Button("Диагностика четырёх камер · без управления").onClick.Invoke());yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.False);
            Assert.That(ground.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("Invalid value/step")),Is.True);
            ground.TeamProfile.Set("spawn.initialOpponentSeparation",5);
            ground.SetMatchMode(NativeMatchMode.Teams);for(int i=0;i<4;i++)ground.SetTeam(i,NativeTeam.TeamA);
            Assert.That(Button("Диагностика четырёх камер · без управления").interactable,Is.False);
            Button("Диагностика четырёх камер · без управления").onClick.Invoke();Assert.That(ground.Running,Is.False);
            ground.SetTeam(3,NativeTeam.TeamB);Button("seats-minus").onClick.Invoke();Assert.That(Button("Диагностика четырёх камер · без управления").interactable,Is.False);
            ground.SetTeam(1,NativeTeam.TeamB);
            Assert.That(ground.GetComponentsInChildren<Button>(true).Any(b=>b.name=="team-colors"),Is.False);
            Button("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.Session.Match.Roster.Count,Is.EqualTo(3));
            var old=ground.Session;var roster=JsonUtility.ToJson(old.Match.Roster.Read());var expected=NativeStandingsView.TeamColor(NativeTeam.TeamA,false);
            AssertIdentity(ground.transform.Find("player-1/trooper-presentation"),expected);AssertIdentity(ground.transform.Find("seat-camera-1/trooper-view"),expected);
            var table=ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="persistent-standings");
            Assert.That(table.gameObject.activeSelf,Is.True);Assert.That(table.GetComponentsInChildren<Text>().Any(t=>t.text=="Team A"),Is.True);
            ground.SetTeam(0,NativeTeam.TeamB);Assert.That(JsonUtility.ToJson(old.Match.Roster.Read()),Is.EqualTo(roster),"Setup mutation ignored while running");
            old.ApplyDamage(1,old.Life(1).Life,500,0,old.Life(0).Life);yield return null;
            var corpse=ground.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("corpse-2-"));AssertIdentity(corpse,NativeStandingsView.TeamColor(NativeTeam.TeamB,false));
            // Exercise result through session time completion, then actual result button.
            while(old.Match.Phase==NativeMatchPhase.Running)old.Tick(new LocalAction[3],1f/ground.Profile.Get("simulation.fixedTickHz"));
            yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.GetComponentsInChildren<Text>(true).Any(t=>t.text.StartsWith("ПОБЕДИЛА Team A")),Is.True);
            Button("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Session,Is.Not.SameAs(old));Assert.That(JsonUtility.ToJson(ground.Session.Match.Roster.Read()),Is.EqualTo(roster));
            Assert.That(ground.Session.Match.Read().Standings.All(r=>r.Score==0),Is.True);Assert.That(ground.Session.Life(1).Health,Is.EqualTo(100));
            AssertIdentity(ground.transform.Find("player-1/trooper-presentation"),expected);
            Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground,3);ground.SetMatchMode(NativeMatchMode.Ffa);Button("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);yield return null;
            Assert.That(ground.Session.Match.Roster.Mode,Is.EqualTo(NativeMatchMode.Ffa));AssertIdentity(ground.transform.Find("player-1/trooper-presentation"),NativeStandingsView.Palette[0]);
            Assert.That(table.GetComponentsInChildren<Text>().Any(t=>t.text=="Team A"||t.text=="Team B"),Is.False);
        }
        static void AssertIdentity(Transform root,Color expected)
        {
            int found=0;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))for(int i=0;i<renderer.sharedMaterials.Length;i++)
                if(renderer.sharedMaterials[i].name.StartsWith("identity",StringComparison.OrdinalIgnoreCase))
                { var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);Assert.That(block.GetColor(renderer.sharedMaterials[i].HasProperty("_BaseColor")?"_BaseColor":"baseColorFactor"),Is.EqualTo(expected));found++; }
            Assert.That(found,Is.GreaterThan(0));
        }
    }
}
