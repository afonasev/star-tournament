using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeNavigationPhysicsTests
    {
        GameObject root;
        ProvingProfile movement, profile;
        ProvingArena arena;
        NativeNavigationProvider provider;
        [SetUp] public void Setup()
        {
            root = new GameObject("navigation-test"); movement = ProvingProfile.CreateDefault(); profile = ProvingProfile.CreateNavigationDefault();
            var a = new GameObject("arena"); a.transform.SetParent(root.transform); arena = a.AddComponent<ProvingArena>(); arena.Build(AuthoredPhysicsFixture.Freeze(),movement);
            provider = new NativeNavigationProvider(arena, movement, profile);
        }
        [UnityTest] public IEnumerator ForeignNavMeshCannotReplaceDisabledOwnedSurface()
        {
            var scene=SceneManager.CreateScene("foreign-navigation",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var other=new GameObject("foreign-arena");SceneManager.MoveGameObjectToScene(other,scene);
            try
            {
                var foreign=other.AddComponent<ProvingArena>();foreign.Build(AuthoredPhysicsFixture.Freeze(),movement);yield return null;
                Assert.That(foreign.Surface.navMeshData,Is.Not.Null);
                Assert.That(provider.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,out _,out var failure),Is.True,failure);
                arena.Surface.enabled=false;
                Assert.That(provider.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,out _,out _),Is.False);
            }
            finally { Object.Destroy(other); }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator DisconnectedFloorsRejectPartialNativeRoute()
        {
            foreach(var collider in arena.GetComponentsInChildren<Collider>())
                if(collider.name=="ramp"||collider.name.StartsWith("stair-"))Object.DestroyImmediate(collider.gameObject);
            Physics.SyncTransforms();arena.Surface.BuildNavMesh();yield return null;
            Assert.That(provider.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,out _,out var failure),Is.False);
            Assert.That(failure,Is.EqualTo("No complete route"));
        }
        [UnityTearDown] public IEnumerator Cleanup() { Object.Destroy(root); yield return null; }
        CharacterMotor Motor(Vector3 feet)
        {
            var go = new GameObject("participant"); go.layer = ProvingArena.ParticipantLayer; go.transform.SetParent(root.transform);
            go.AddComponent<CharacterController>(); var motor = go.AddComponent<CharacterMotor>(); motor.Initialize(movement, feet); return motor;
        }
        [UnityTest] public IEnumerator GeneratedFamilies_DeclaredTransitions_ThreeLanesBothDirections()
        {
            // Each fixture owns the only valid supports. Building them one at a time also exercises the single active context contract.
            Object.DestroyImmediate(arena.gameObject); yield return null;
            for (int family = 0; family < 1; family++)
            {
                var familyRoot = new GameObject("navigation-family-" + family); familyRoot.transform.SetParent(root.transform);
                var frozen = CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault()); var definition=frozen.Definition;
                var familyArena = familyRoot.AddComponent<ProvingArena>(); familyArena.Build(frozen, movement);
                var familyProvider = new NativeNavigationProvider(familyArena, movement, profile);
                foreach (var transition in familyArena.ReadNavigationTransitions())
                for (int direction = -1; direction <= 1; direction += 2)
                for (int lane = -1; lane <= 1; lane++)
                {
                    Vector3 lower = transition.OrderedFeet[0], upper = transition.OrderedFeet[transition.OrderedFeet.Length - 1];
                    Vector3 lateral = Vector3.Cross(Vector3.up, upper - lower); lateral.y = 0; lateral.Normalize();
                    Vector3 forward = upper - lower; forward.y = 0; forward.Normalize();
                    Vector3 bottom = lower - forward + lateral * lane * movement.Get("player.capsule.radius");
                    Vector3 top = upper + forward + lateral * lane * movement.Get("player.capsule.radius");
                    Vector3 start = direction > 0 ? bottom : top;
                    Vector3 goal = direction > 0 ? top : bottom;
                    Assert.That(familyProvider.TryRoute(start, goal, out var route, out var failure), Is.True, definition.MapId + " " + transition.Id + " " + failure);
                    Assert.That(route, Has.Some.Matches<NativeNavigationPoint>(p => p.Support == transition.Id));
                    var motor = Motor(start); var bot = new NativeBotNavigation(familyProvider, profile); bot.SetStaticGoal(goal);
                    var own = new NativeBotKnowledge { Alive = true, OwnLife = 1 }; int recoveries = 0;
                    for (int tick = 0; tick < 1800 && bot.Status != NativeNavigationStatus.Arrived && bot.Status != NativeNavigationStatus.Blocked; tick++)
                    {
                        var previous = motor.State.Position; var action = bot.Tick(tick / 60d, motor.State, own);
                        Assert.That(action.Jump, Is.False); motor.Tick(action, 1 / 60f); Physics.SyncTransforms();
                        Assert.That(Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(motor.State.Position.x, motor.State.Position.z)),
                            Is.LessThan(movement.Get("player.movement.maximumGroundSpeed") / 60f + .02f), "Horizontal movement must remain motor-bounded; stair rise is physical contact.");
                        if (motor.State.Grounded)
                        {
                            Assert.That(familyProvider.TryPhysicalSupport(motor.State.Position, out var support), Is.True, definition.MapId + " lost support");
                            Assert.That(support.Support == transition.LowerSupport || support.Support == transition.UpperSupport || support.Support == transition.Id, Is.True);
                        }
                        recoveries = Mathf.Max(recoveries, bot.Capture().Recoveries);
                    }
                    Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Arrived), definition.MapId + " " + transition.Id + " direction=" + direction + " lane=" + lane + " pose=" + motor.State.Position + " " + bot.Capture().Failure);
                    Assert.That(recoveries, Is.Zero, definition.MapId + " free transition recovered");
                    Assert.That(motor.State.Position.y, Is.EqualTo(goal.y).Within(profile.Get("bots.navigation.heightTolerance")));
                    Object.DestroyImmediate(motor.gameObject);
                }
                Object.DestroyImmediate(familyRoot); yield return null;
            }
        }
        [UnityTest] public IEnumerator StairsAndRamp_ThreeLanesBothDirections_WithMidwayRepath()
        {
            yield return null;
            float w = AuthoredPhysicsFixture.Value("fixture.width"), rw = AuthoredPhysicsFixture.Value("fixture.rampWidth"), wt = AuthoredPhysicsFixture.Value("fixture.wallThickness");
            float h = AuthoredPhysicsFixture.Value("fixture.upperFloorHeight"), low = AuthoredPhysicsFixture.Value("fixture.floorHeight");
            float stairsRun = Mathf.CeilToInt((h-low)/AuthoredPhysicsFixture.Value("fixture.stairRise")) * AuthoredPhysicsFixture.Value("fixture.stairTread");
            for (int side = -1; side <= 1; side += 2)
            for (int lane = -1; lane <= 1; lane++)
            for (int direction = -1; direction <= 1; direction += 2)
            {
                float x = side * (w/2-rw/2-wt) + lane * movement.Get("player.capsule.radius");
                var bottom = new Vector3(x, low, -(side<0 ? stairsRun : AuthoredPhysicsFixture.Value("fixture.rampLength"))-1);
                var top = new Vector3(x, h, 1);
                var motor = Motor(direction > 0 ? bottom : top); var goal = direction > 0 ? top : bottom;
                var bot = new NativeBotNavigation(provider, profile); bot.SetStaticGoal(goal);
                var own = new NativeBotKnowledge { Alive = true, OwnLife = 1 }; int maxRecovery = 0; bool restored=false;
                float earlySpeed=0,lateSpeed=0;int earlyCount=0,lateCount=0;
                for (int i = 0; i < 1800 && bot.Status != NativeNavigationStatus.Arrived && bot.Status != NativeNavigationStatus.Blocked; i++)
                {
                    var action = bot.Tick(i/60d, motor.State, own); Assert.That(action.Jump, Is.False);
                    motor.Tick(action, 1/60f); Physics.SyncTransforms();
                    if(direction>0)
                    {
                        float speed=new Vector2(motor.State.Velocity.x,motor.State.Velocity.z).magnitude;
                        if(motor.State.Position.y>h*.2f&&motor.State.Position.y<h*.4f){earlySpeed+=speed;earlyCount++;}
                        if(motor.State.Position.y>h*.6f&&motor.State.Position.y<h*.8f){lateSpeed+=speed;lateCount++;}
                    }
                    if (!restored && motor.State.Position.y>h*.25f && motor.State.Position.y<h*.75f && !string.IsNullOrEmpty(bot.Capture().ActiveTransition))
                    { bot.Restore(bot.Capture());restored=true; } // Confirmed middle of this physical transition.
                    maxRecovery = Mathf.Max(maxRecovery, bot.Capture().Recoveries);
                }
                Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Arrived), $"side={side} lane={lane} direction={direction} pose={motor.State.Position} failure={bot.Capture().Failure}");
                Assert.That(maxRecovery, Is.Zero, "Free transition required recovery");
                Assert.That(restored,Is.True,"No confirmed mid-transition restore");
                if(direction>0)
                {
                    Assert.That(earlyCount,Is.GreaterThan(0));Assert.That(lateCount,Is.GreaterThan(0));
                    Assert.That(lateSpeed/lateCount,Is.GreaterThanOrEqualTo(earlySpeed/earlyCount*.8f),"Cumulative ascent slowdown");
                    Debug.Log($"NAVIGATION_ASCENT side={side} lane={lane} earlyXZ={earlySpeed/earlyCount:F3} lateXZ={lateSpeed/lateCount:F3}");
                }
                Assert.That(motor.State.Position.y, Is.EqualTo(goal.y).Within(profile.Get("bots.navigation.heightTolerance")));
                Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator ChangedGoalDuringTransition_FinishesSelectedDirectionBeforeReturning()
        {
            yield return null;
            foreach(int side in new[]{-1,1})
            {
                float x=side*(AuthoredPhysicsFixture.Value("fixture.width")/2-AuthoredPhysicsFixture.Value("fixture.rampWidth")/2-AuthoredPhysicsFixture.Value("fixture.wallThickness"));
                var start=new Vector3(x,0,-13);var top=new Vector3(x,AuthoredPhysicsFixture.Value("fixture.upperFloorHeight"),1);
                var motor=Motor(start);var bot=new NativeBotNavigation(provider,profile);bot.SetStaticGoal(top);
                var own=new NativeBotKnowledge{Alive=true,OwnLife=1};bool reversed=false;float maxY=0;
                for(int i=0;i<3600 && bot.Status!=NativeNavigationStatus.Blocked;i++)
                {
                    motor.Tick(bot.Tick(i/60d,motor.State,own),1/60f);Physics.SyncTransforms();maxY=Mathf.Max(maxY,motor.State.Position.y);
                    if(!reversed&&motor.State.Position.y>1.5f&&!string.IsNullOrEmpty(bot.Capture().ActiveTransition))
                    {bot.SetStaticGoal(start);bot.Restore(bot.Capture());reversed=true;}
                    if(reversed&&bot.Status==NativeNavigationStatus.Arrived)break;
                }
                Assert.That(reversed,Is.True);Assert.That(maxY,Is.GreaterThanOrEqualTo(top.y-profile.Get("bots.navigation.heightTolerance")));
                Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),bot.Capture().Failure);
                Assert.That(motor.State.Position.y,Is.LessThan(.3f));Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator OverlappingFloors_UseDeclaredTransition_AndInvalidGoalIsRejected()
        {
            yield return null;
            var bottom = new Vector3(0, 0, 5); var top = new Vector3(0, AuthoredPhysicsFixture.Value("fixture.upperFloorHeight"), 5);
            Assert.That(provider.TryRoute(bottom, top, out var route, out var failure), Is.True, failure);
            Assert.That(route.Length, Is.GreaterThan(2));
            Assert.That(provider.TryRoute(bottom, new Vector3(0, 1.5f, 5), out _, out _), Is.False);
            Assert.That(provider.TryRoute(bottom, new Vector3(100, 0, 100), out _, out _), Is.False);
            // A second arena's NavMesh cannot turn a foreign endpoint into an owned support.
            var foreign = GameObject.CreatePrimitive(PrimitiveType.Cube); foreign.transform.SetParent(root.transform);
            foreign.layer = ProvingArena.WorldLayer; foreign.transform.position = bottom + Vector3.up * .1f;
            foreign.transform.localScale = new Vector3(2,.1f,2); Physics.SyncTransforms();
            Assert.That(provider.TryRoute(bottom + Vector3.up*.15f, top, out _, out _), Is.False);
        }
        [UnityTest] public IEnumerator LivingBlocker_RemainsSolidDuringBoundedRecovery()
        {
            yield return null;
            var motor = Motor(new Vector3(0,0,-12)); var blocker = Motor(new Vector3(0,0,-10)); var fixedPosition = blocker.State.Position;
            var bot = new NativeBotNavigation(provider, profile); bot.SetStaticGoal(new Vector3(0,0,-8));
            var own = new NativeBotKnowledge { Alive=true, OwnLife=1 }; bool recovered = false;
            for (int i=0;i<1200 && bot.Status!=NativeNavigationStatus.Arrived && bot.Status!=NativeNavigationStatus.Blocked;i++)
            {
                motor.Tick(bot.Tick(i/60d,motor.State,own),1/60f); Physics.SyncTransforms();
                recovered |= bot.Status==NativeNavigationStatus.Recovering;
                Assert.That(Vector2.Distance(new Vector2(motor.State.Position.x,motor.State.Position.z),new Vector2(fixedPosition.x,fixedPosition.z)),
                    Is.GreaterThanOrEqualTo(movement.Get("player.capsule.radius")*2-movement.Get("player.capsule.skinWidth")*2));
            }
            Assert.That(recovered, Is.True); Assert.That(blocker.State.Position, Is.EqualTo(fixedPosition));
            Assert.That(bot.Status == NativeNavigationStatus.Arrived || bot.Status == NativeNavigationStatus.Blocked, Is.True);
        }
        [UnityTest] public IEnumerator NarrowOccupiedCorridor_TerminatesWithoutPushingOrTeleporting()
        {
            yield return null;
            // Test-only enclosure preserves a direct static route, but two living capsules cannot pass.
            foreach(float x in new[]{-.8f,.8f})
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.layer=ProvingArena.WorldLayer;
                wall.transform.position=new Vector3(x,1.5f,-10);wall.transform.localScale=new Vector3(.2f,3,8);
            }
            Physics.SyncTransforms();var motor=Motor(new Vector3(0,0,-12));var blocker=Motor(new Vector3(0,0,-10));
            var bot=new NativeBotNavigation(provider,profile);bot.SetStaticGoal(new Vector3(0,0,-8));var own=new NativeBotKnowledge{Alive=true,OwnLife=1};bool recovery=false;
            for(int i=0;i<1800&&bot.Status!=NativeNavigationStatus.Blocked;i++)
            {
                var previous=motor.State.Position;motor.Tick(bot.Tick(i/60d,motor.State,own),1/60f);Physics.SyncTransforms();recovery|=bot.Status==NativeNavigationStatus.Recovering;
                Assert.That(Vector3.Distance(previous,motor.State.Position),Is.LessThan(movement.Get("player.movement.maximumGroundSpeed")/60f+.02f));
                Assert.That(Vector3.Distance(motor.State.Position,blocker.State.Position),Is.GreaterThan(1.05f));
            }
            Assert.That(recovery,Is.True);Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Blocked));
            Assert.That(blocker.State.Position,Is.EqualTo(new Vector3(0,0,-10)));
        }
    }
}
