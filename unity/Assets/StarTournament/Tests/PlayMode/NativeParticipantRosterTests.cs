using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeParticipantRosterTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
        }
        public static NativeMatchComposition Mixed(int humans,bool teams=false)
        {
            int[] mapping=Enumerable.Range(8-humans,humans).Reverse().ToArray();
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,8).Select(i=>i%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray()):NativeMatchRoster.Ffa(8);
            var metadata=Enumerable.Range(0,8).Select(p=>new NativeParticipantInfo(mapping.Contains(p)?NativeParticipantKind.LocalHuman:NativeParticipantKind.DiagnosticFixture,
                "Участник "+(p+1),NativeStandingsView.Identity(roster.Read(),p,false))).ToArray();
            return new NativeMatchComposition(roster,metadata,mapping);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator EightActorsUseOnlyMappedLocalViewsAndRepeatDisposesOldObjects()
        {
            yield return Load();int devices=InputSystem.devices.Count;
            foreach(int humans in new[]{4,1,3,4})
            {
                ground.StartParticipantReview(Mixed(humans,humans==3));yield return null;yield return new WaitForFixedUpdate();yield return null;
                Assert.That(ground.Running,Is.True,"Eight-participant placement must succeed");
                Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));Assert.That(ground.Composition.ParticipantAt(0),Is.EqualTo(7));
                Assert.That(InputSystem.devices.Count,Is.EqualTo(devices),"Nonlocal actors have no fake devices");
                Assert.That(ground.GetComponentsInChildren<CharacterController>().Count(c=>c.enabled),Is.EqualTo(8));
                Assert.That(ground.GetComponentsInChildren<Camera>(true).Length,Is.EqualTo(humans));
                Assert.That(ground.GetComponentsInChildren<TrooperVisual>(true).Count(v=>v.name=="trooper-view"),Is.EqualTo(humans));
                Assert.That(ground.GetComponentsInChildren<Camera>().First(c=>c.name=="seat-camera-1").rect,Is.EqualTo(LocalSeatLayout.Viewport(humans,0)));
                var snapshot=JsonUtility.ToJson(ground.Composition.Read());var old=ground.Session;
                ground.Profile.Set("camera.fieldOfViewDegrees",100);Button("Повторить матч").onClick.Invoke();yield return null;
                Assert.That(ground.Session,Is.Not.SameAs(old));Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(snapshot));
                Assert.That(ground.Session.Match.Read().Standings,Has.Length.EqualTo(8));
                Assert.That(ground.GetComponentsInChildren<Camera>(true).Length,Is.EqualTo(humans));
            }
            Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();yield return null;
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator SeatZeroInputMovesParticipantSevenAndNonlocalKillerOwnsKillcam()
        {
            yield return Load();pad=InputSystem.AddDevice<Gamepad>();ground.StartParticipantReview(Mixed(1),new InputDevice[]{pad});yield return null;
            Assert.That(ground.Running,Is.True);ground.PlaceCombatReviewSeat(7,new Vector3(0,0,-8),0);
            yield return new WaitForFixedUpdate();var before=ground.Session.Pose(7).Position;var other=ground.Session.Pose(0).Position;
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});yield return null;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(ground.Session.Pose(7).Position.x,Is.GreaterThan(before.x));Assert.That(ground.Session.Pose(0).Position.x,Is.EqualTo(other.x));
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            ground.Session.ApplyDamage(7,ground.Session.Life(7).Life,500,0,ground.Session.Life(0).Life);yield return null;
            var camera=ground.GetComponentsInChildren<Camera>().Single();
            var target=ground.Session.Pose(0).Position+Vector3.up*ground.Profile.Get("camera.eyeHeight");
            Assert.That(Vector3.Angle(camera.transform.forward,target-camera.transform.position),Is.LessThan(.1f));
            var notice=ground.transform.Find("native-ui/seat-viewport-0/kill-notice-0").GetComponent<Text>().text;
            Assert.That(notice,Does.Contain("Участник 1"));
            Assert.That(notice,Does.Contain("<color=#"));
            var ownCorpse=ground.transform.Find("corpse-8-life-1");Assert.That(ownCorpse,Is.Not.Null);
            Assert.That(camera.cullingMask&(1<<ownCorpse.gameObject.layer),Is.Zero,"Own corpse cannot occlude killer tracking");
            var direction=camera.transform.forward;ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,500);yield return null;
            Assert.That(Vector3.Angle(direction,camera.transform.forward),Is.LessThan(.1f));
            Assert.That(ground.GetComponentsInChildren<Collider>().All(c=>!c.transform.name.StartsWith("corpse-")),Is.True);
        }
        [UnityTest] public IEnumerator InvalidDraftRollsBackToSafeSetupAndCanBeCorrected()
        {
            yield return Load();
            foreach(var item in new[]{(ground.Profile,"player.capsule.height"),(ground.CombatProfile,"shot.spread")})
            {
                float original=item.Item1.Get(item.Item2);item.Item1.Set(item.Item2,-1);
                Assert.DoesNotThrow(()=>ground.StartParticipantReview(Mixed(1)));
                yield return null;Assert.That(ground.Running,Is.False);
                Assert.That(ground.transform.Find("native-ui/setup-pause/menu/status").GetComponent<Text>().text,Does.Contain("Invalid"));
                Assert.That(ground.GetComponentsInChildren<CharacterController>().Length,Is.EqualTo(ground.LocalSeatCount));
                item.Item1.Set(item.Item2,original);ground.StartParticipantReview(Mixed(1));yield return null;
                Assert.That(ground.Running,Is.True);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));
                Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground,2);yield return null;
            }
        }
        [UnityTest] public IEnumerator TeamTotalUsesFrozenCompositionColors()
        {
            yield return Load();var snapshot=Mixed(3,true).Read();
            for(int p=0;p<8;p++)snapshot.Participants[p].Color=NativeStandingsView.TeamColor(snapshot.Roster.Teams[p],true);
            ground.StartParticipantReview(NativeMatchComposition.Restore(snapshot));yield return null;
            var table=ground.transform.Find("native-ui/persistent-standings");
            var total=table.Find("row-1/cell-0").GetComponent<Text>();Assert.That(total.text,Is.EqualTo("Team A"));
            Assert.That(total.color,Is.EqualTo(NativeStandingsView.TeamColor(NativeTeam.TeamA,true)));
            Assert.That(table.GetComponentsInChildren<Text>().Count(t=>t.text.Contains("FIXTURE")||t.text.Contains("ИГРОК")),Is.EqualTo(8));
        }
        [UnityTest] public IEnumerator EightSlotAllocatorHasBoundedAtomicFailure()
        {
            yield return Load();
            foreach(var motor in ground.GetComponentsInChildren<CharacterMotor>())motor.SetAlive(false);Physics.SyncTransforms();
            var arena=ground.GetComponentInChildren<ProvingArena>();var selector=new SafeSpawnSelector(scene.GetPhysicsScene(),arena,ground.Profile,ground.CombatProfile);
            foreach(var roster in new[]{NativeMatchRoster.Ffa(8),new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB,NativeTeam.TeamB,NativeTeam.TeamB,NativeTeam.TeamB,NativeTeam.TeamB,NativeTeam.TeamB})})
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();Assert.That(selector.TryInitial(roster,5,out var positions),Is.True,selector.InitialFailure);timer.Stop();
                Assert.That(positions.Distinct().Count(),Is.EqualTo(8));foreach(var point in positions)Assert.That(selector.Valid(point,out _),Is.True);
                Debug.Log($"ROSTER_SPAWN_DIAGNOSTIC nodes={selector.InitialSearchNodes} elapsedMs={timer.Elapsed.TotalMilliseconds:F3}");
            }
            Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(8),5,out var limited,1),Is.False);Assert.That(limited,Is.Empty);
            Assert.That(selector.InitialFailure,Is.EqualTo("SearchBudgetExceeded"));Assert.That(selector.InitialSearchNodes,Is.EqualTo(1));
            Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(8),float.MaxValue,out var impossible),Is.False);Assert.That(impossible,Is.Empty);
            Assert.That(selector.InitialFailure,Is.EqualTo("NoValidPlacement"));
            var foundation=ProvingProfile.CreateDefault();
            var dense=ProvingProfile.CreateNativeCombatDefault();
            var wideRoot=new GameObject("wide-foundation");SceneManager.MoveGameObjectToScene(wideRoot,scene);var wideArena=wideRoot.AddComponent<ProvingArena>();wideArena.Build(CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault()),ground.Profile);
            var stress=new SafeSpawnSelector(scene.GetPhysicsScene(),wideArena,ground.Profile,dense);var watch=System.Diagnostics.Stopwatch.StartNew();
            Assert.That(stress.TryInitial(NativeMatchRoster.Ffa(8),1,out var oversized,100,4),Is.False);watch.Stop();
            Assert.That(oversized,Is.Empty);Assert.That(stress.InitialFailure,Is.EqualTo("CandidateBudgetExceeded"));Assert.That(stress.InitialSearchNodes,Is.Zero);
            Object.Destroy(wideRoot);
            Debug.Log($"ROSTER_CANDIDATE_BUDGET_DIAGNOSTIC elapsedMs={watch.Elapsed.TotalMilliseconds:F3}");
        }
    }
}
