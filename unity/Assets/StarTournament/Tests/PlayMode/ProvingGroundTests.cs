using System;
using System.Collections;
using System.Linq;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    /// <summary>Physical proving-ground checks. These assert resulting contacts and routes, not motor internals.</summary>
    public sealed class ProvingGroundTests
    {
        private ProvingProfile profile;
        private GameObject arenaRoot;
        private Scene testScene;

        [SetUp]
        public void SetUp()
        {
            profile = ProvingProfile.CreateDefault();
            testScene = SceneManager.CreateScene("proving-ground-physical-test-" + Guid.NewGuid(), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        [TearDown]
        public void TearDown()
        {
            if (arenaRoot != null) Object.DestroyImmediate(arenaRoot);
            if (testScene.IsValid()) SceneManager.UnloadSceneAsync(testScene);
        }

        [UnityTest]
        public IEnumerator LatestPackagedDefault_DrivesPhysicalAccelerationBrakingAndJump()
        {
            var latest = LabReleaseCatalog.Load().Entries
                .Where(e => e.ProfileId == DesignLabHistory.ReleaseId)
                .OrderByDescending(e => e.Sequence).First();
            profile = latest.Snapshot.Clone().Profile("player.movement.maximumGroundSpeed");
            Assert.That(profile.Validate(), Is.Empty);
            arenaRoot = new GameObject("packaged-default-movement");
            SceneManager.MoveGameObjectToScene(arenaRoot, testScene);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(arenaRoot.transform);
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(100, 1, 100);
            floor.layer = ProvingArena.WorldLayer;
            var motor = CreateMotor(Vector3.zero);
            yield return Settle(motor);
            Assert.That(motor.State.Grounded, Is.True);
            float dt = Time.fixedDeltaTime;
            motor.Tick(new LocalAction { Move = Vector2.up }, dt);
            Assert.That(motor.State.Velocity.z,
                Is.EqualTo(profile.Get("player.movement.groundAcceleration") * dt).Within(.02f));
            yield return new WaitForFixedUpdate();
            yield return MoveWorld(motor, Vector3.forward, 30);
            Assert.That(motor.State.Velocity.z,
                Is.EqualTo(profile.Get("player.movement.maximumGroundSpeed")).Within(.02f));
            float speed = motor.State.Velocity.z;
            motor.Tick(default, dt);
            Assert.That(motor.State.Velocity.z,
                Is.EqualTo(Mathf.Max(0, speed - profile.Get("player.movement.groundDeceleration") * dt)).Within(.02f));
            yield return new WaitForFixedUpdate();
            for (int tick = 0; tick < 30; tick++)
            {
                motor.Tick(default, dt);
                yield return new WaitForFixedUpdate();
            }
            float startY = motor.State.Position.y, highestY = startY;
            bool airborne = false;
            for (int tick = 0; tick < 100; tick++)
            {
                motor.Tick(new LocalAction { Jump = tick == 0 }, dt);
                highestY = Mathf.Max(highestY, motor.State.Position.y);
                airborne |= !motor.State.Grounded;
                yield return new WaitForFixedUpdate();
            }
            float jumpSpeed = profile.Get("player.movement.jumpSpeed");
            float expectedHeight = jumpSpeed * jumpSpeed / (2 * profile.Get("player.movement.gravity"));
            Assert.That(airborne, Is.True);
            Assert.That(highestY - startY, Is.EqualTo(expectedHeight).Within(jumpSpeed * dt + .05f));
            Assert.That(motor.State.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator FirstIdleTick_PreservesRequestedSpawnXZ()
        {
            BuildArena();
            yield return null;

            var spawn = new Vector3(3f, AuthoredPhysicsFixture.Value("fixture.floorHeight"), 2f);
            var motor = CreateMotor(spawn);
            motor.Tick(default, Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();

            var horizontalError = Vector2.Distance(
                new Vector2(motor.State.Position.x, motor.State.Position.z),
                new Vector2(spawn.x, spawn.z));
            Assert.That(horizontalError, Is.LessThanOrEqualTo(profile.Get("player.capsule.skinWidth") * 2f));
        }

        [UnityTest]
        public IEnumerator MovementBarrierDoesNotOccludeShotsButWindowDoes()
        {
            BuildArena();yield return null;
            // Query through the barrier body; eye height intentionally lies above this low barrier.
            var origin=new Vector3(-AuthoredPhysicsFixture.Value("fixture.rampWidth")*1.5f,AuthoredPhysicsFixture.Value("fixture.wallHeight")/4,-AuthoredPhysicsFixture.Value("fixture.depth")/4);
            var physics=testScene.GetPhysicsScene();
            Assert.That(physics.Raycast(origin,Vector3.right,out var movementHit,profile.Get("weapon.probeRange"),~0),Is.True);
            Assert.That(movementHit.collider.name,Is.EqualTo("movement-only-barrier"));
            Assert.That(physics.Raycast(origin,Vector3.right,out var shotHit,profile.Get("weapon.probeRange"),ProvingArena.ShotMask),Is.True);
            Assert.That(shotHit.collider.name,Is.EqualTo("solid-window-probe"));
        }

        [UnityTest]
        public IEnumerator Wall_Headroom_AndLiveParticipantBlockPhysicalMotion()
        {
            var arena = BuildArena();
            yield return null;

            var wallMotor = CreateMotor(new Vector3(0f, AuthoredPhysicsFixture.Value("fixture.floorHeight"), -AuthoredPhysicsFixture.Value("fixture.depth") * .5f + 2f));
            yield return Settle(wallMotor);
            yield return MoveWorld(wallMotor, Vector3.back, 160);
            var wallInnerFace = -AuthoredPhysicsFixture.Value("fixture.depth") * .5f + AuthoredPhysicsFixture.Value("fixture.wallThickness") * .5f;
            Assert.That(wallMotor.State.Position.z, Is.GreaterThanOrEqualTo(wallInnerFace + profile.Get("player.capsule.radius") - .08f));

            var headroomMotor = CreateMotor(new Vector3(0f, AuthoredPhysicsFixture.Value("fixture.floorHeight"), -AuthoredPhysicsFixture.Value("fixture.depth") * .25f));
            yield return Settle(headroomMotor);
            var highestHead = headroomMotor.State.Position.y + profile.Get("player.capsule.height");
            for (var tick = 0; tick < 100; tick++)
            {
                headroomMotor.Tick(new LocalAction { Jump = tick == 0 }, Time.fixedDeltaTime);
                highestHead = Mathf.Max(highestHead, headroomMotor.State.Position.y + profile.Get("player.capsule.height"));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(highestHead, Is.LessThanOrEqualTo(AuthoredPhysicsFixture.Value("fixture.headroomHeight") + profile.Get("player.capsule.skinWidth") + .08f));

            var blocker = CreateMotor(new Vector3(0f, AuthoredPhysicsFixture.Value("fixture.floorHeight"), 4f));
            var mover = CreateMotor(new Vector3(-3f, AuthoredPhysicsFixture.Value("fixture.floorHeight"), 4f));
            var maximumMoverX = mover.State.Position.x;
            yield return Settle(blocker);
            yield return Settle(mover);
            var blockerStart = blocker.State.Position;
            yield return MoveWorld(mover, Vector3.right, 160, state => maximumMoverX = Mathf.Max(maximumMoverX, state.Position.x));
            var horizontalDistance = Vector2.Distance(
                new Vector2(mover.State.Position.x, mover.State.Position.z),
                new Vector2(blocker.State.Position.x, blocker.State.Position.z));
            Assert.That(horizontalDistance, Is.GreaterThanOrEqualTo(profile.Get("player.capsule.radius") * 2f - .08f));
            Assert.That(maximumMoverX, Is.LessThan(blockerStart.x), "The moving capsule must never pass through the stationary participant.");
            Assert.That(Vector3.Distance(blocker.State.Position, blockerStart), Is.LessThanOrEqualTo(.001f));
            Assert.That(arena.Spawns.Length, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator StairsAndRamp_AreTraversableInBothDirections()
        {
            BuildArena();
            yield return null;

            var stairs = arenaRoot.transform.Find("stair-0");
            var lastStair = arenaRoot.transform.Find("stair-" + (Mathf.CeilToInt((AuthoredPhysicsFixture.Value("fixture.upperFloorHeight") - AuthoredPhysicsFixture.Value("fixture.floorHeight")) / AuthoredPhysicsFixture.Value("fixture.stairRise")) - 1));
            Assert.That(stairs, Is.Not.Null);
            Assert.That(lastStair, Is.Not.Null);

            var stairsUp = CreateMotor(stairs.position + Vector3.back * AuthoredPhysicsFixture.Value("fixture.stairTread") * .45f + Vector3.up * AuthoredPhysicsFixture.Value("fixture.floorThickness"));
            yield return Settle(stairsUp);
            yield return MoveWorld(stairsUp, Vector3.forward, 220);
            Assert.That(stairsUp.State.Position.y, Is.GreaterThanOrEqualTo(AuthoredPhysicsFixture.Value("fixture.upperFloorHeight") - .2f));
            Object.Destroy(stairsUp.gameObject);
            yield return null;

            var stairsDown = CreateMotor(lastStair.position + Vector3.forward * AuthoredPhysicsFixture.Value("fixture.stairTread") * .45f + Vector3.up * (AuthoredPhysicsFixture.Value("fixture.upperFloorHeight") - lastStair.position.y + AuthoredPhysicsFixture.Value("fixture.floorThickness")));
            yield return Settle(stairsDown);
            yield return MoveWorld(stairsDown, Vector3.back, 220);
            Assert.That(stairsDown.State.Position.y, Is.LessThanOrEqualTo(AuthoredPhysicsFixture.Value("fixture.floorHeight") + .2f));
            Object.Destroy(stairsDown.gameObject);
            yield return null;

            var ramp = arenaRoot.transform.Find("ramp");
            Assert.That(ramp, Is.Not.Null);
            var first = ramp.TransformPoint(new Vector3(0f, .5f, -.5f));
            var second = ramp.TransformPoint(new Vector3(0f, .5f, .5f));
            var low = first.y < second.y ? first : second;
            var high = first.y < second.y ? second : first;
            var ascent = Vector3.ProjectOnPlane(high - low, Vector3.up).normalized;

            var rampUp = CreateMotor(low - ascent * .35f + Vector3.up * AuthoredPhysicsFixture.Value("fixture.floorThickness"));
            yield return Settle(rampUp);
            yield return MoveWorld(rampUp, ascent, 220);
            Assert.That(rampUp.State.Position.y, Is.GreaterThanOrEqualTo(high.y - AuthoredPhysicsFixture.Value("fixture.floorThickness") - .2f));
            Object.Destroy(rampUp.gameObject);
            yield return null;

            var rampDown = CreateMotor(high + ascent * .1f + Vector3.up * AuthoredPhysicsFixture.Value("fixture.floorThickness"));
            yield return Settle(rampDown);
            yield return MoveWorld(rampDown, -ascent, 220);
            Assert.That(rampDown.State.Position.y, Is.LessThanOrEqualTo(low.y + AuthoredPhysicsFixture.Value("fixture.floorThickness") + .2f));
        }

        [UnityTest]
        public IEnumerator CharacterMotor_FollowsCalculatedNavMeshRouteToUpperSupport()
        {
            var arena = BuildArena();
            yield return null;

            Assert.That(arena.TryRoute(arena.LowerRoutePoint, arena.UpperRoutePoint, profile.Get("navigation.sampleDistance"), out NavMeshPath route), Is.True);
            Assert.That(route.corners.Length, Is.GreaterThan(2));
            var motor = CreateMotor(route.corners[0]);
            yield return Settle(motor);

            var cornerIndex = 1;
            var stalledTicks = 0;
            var previousDistance = float.PositiveInfinity;
            const int maximumTicks = 1400;
            for (var tick = 0; tick < maximumTicks && cornerIndex < route.corners.Length; tick++)
            {
                var target = route.corners[cornerIndex];
                var toTarget = Vector3.ProjectOnPlane(target - motor.State.Position, Vector3.up);
                var distance = toTarget.magnitude;
                if (distance <= profile.Get("player.capsule.radius") * .5f)
                {
                    cornerIndex++;
                    previousDistance = float.PositiveInfinity;
                    stalledTicks = 0;
                    continue;
                }

                var desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                motor.Tick(new LocalAction
                {
                    Move = Vector2.up,
                    LookDegrees = new Vector2(Mathf.DeltaAngle(motor.State.Yaw, desiredYaw), 0f),
                }, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();

                if (distance < previousDistance - profile.Get("player.capsule.skinWidth") * .25f) stalledTicks = 0;
                else stalledTicks++;
                Assert.That(stalledTicks, Is.LessThan(75), $"Motor stalled before route corner {cornerIndex} at {motor.State.Position}.");
                previousDistance = distance;
            }

            Assert.That(cornerIndex, Is.EqualTo(route.corners.Length), "Motor did not complete the calculated lower-to-upper route within its bounded tick budget.");
            var horizontalError = Vector2.Distance(
                new Vector2(motor.State.Position.x, motor.State.Position.z),
                new Vector2(arena.UpperRoutePoint.x, arena.UpperRoutePoint.z));
            Assert.That(horizontalError, Is.LessThanOrEqualTo(profile.Get("player.capsule.radius") * .5f));
            Assert.That(motor.State.Position.y, Is.EqualTo(arena.UpperRoutePoint.y).Within(profile.Get("player.capsule.skinWidth") * 2f));
            Assert.That(motor.State.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator NavigationRoute_UsesPhysicalTransitionInsteadOfSlabShortcut()
        {
            var arena = BuildArena();
            yield return null;

            Assert.That(arena.TryRoute(arena.LowerRoutePoint, arena.UpperRoutePoint, profile.Get("navigation.sampleDistance"), out NavMeshPath route), Is.True);
            Assert.That(route.corners.Length, Is.GreaterThan(2));

            var transitionX = AuthoredPhysicsFixture.Value("fixture.width") * .5f - AuthoredPhysicsFixture.Value("fixture.rampWidth") * .5f - AuthoredPhysicsFixture.Value("fixture.wallThickness");
            var passesTransition = false;
            for (var index = 0; index < route.corners.Length; index++)
                passesTransition |= Mathf.Abs(route.corners[index].x) >= transitionX - AuthoredPhysicsFixture.Value("fixture.rampWidth");
            Assert.That(passesTransition, Is.True, "A lower-to-upper route must detour through a ramp or stairs, never cross the overlapping slab directly.");
        }

        private ProvingArena BuildArena()
        {
            arenaRoot = new GameObject("proving-ground-test-arena");
            SceneManager.MoveGameObjectToScene(arenaRoot, testScene);
            var arena = arenaRoot.AddComponent<ProvingArena>();
            arena.Build(AuthoredPhysicsFixture.Freeze(),profile);
            return arena;
        }

        private CharacterMotor CreateMotor(Vector3 spawn)
        {
            var participant = new GameObject("test-participant");
            participant.layer = ProvingArena.ParticipantLayer;
            participant.transform.SetParent(arenaRoot.transform, true);
            participant.AddComponent<CharacterController>();
            var motor = participant.AddComponent<CharacterMotor>();
            motor.Initialize(profile, spawn);
            return motor;
        }

        private static IEnumerator Settle(CharacterMotor motor)
        {
            // The test cadence follows Unity's configured fixed timestep; it is not a balance value.
            for (var tick = 0; tick < 4; tick++)
            {
                motor.Tick(default, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator MoveWorld(CharacterMotor motor, Vector3 worldDirection, int ticks, Action<ParticipantState> observe = null)
        {
            var normalizedDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
            var desiredYaw = Mathf.Atan2(normalizedDirection.x, normalizedDirection.z) * Mathf.Rad2Deg;
            motor.Tick(new LocalAction { LookDegrees = new Vector2(Mathf.DeltaAngle(motor.State.Yaw, desiredYaw), 0f) }, Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
            for (var tick = 0; tick < ticks; tick++)
            {
                motor.Tick(new LocalAction { Move = Vector2.up }, Time.fixedDeltaTime);
                observe?.Invoke(motor.State);
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
