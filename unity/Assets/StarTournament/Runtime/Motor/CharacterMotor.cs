using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround
{
    /// <summary>
    /// The sole owner of a participant Transform's gameplay movement. Cameras, models and
    /// navigation read State; they must not move this Transform or its CharacterController.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        private CharacterController controller;
        private ProvingProfile profile;
        private ProvingArena arena;
        private ParticipantState state;
        private bool initialized;
        private float horizontalSpeedMultiplier=1f;

        public ParticipantState State => state;
        public bool AcceptedJumpThisTick { get; private set; }

        public void Initialize(ProvingProfile provingProfile, Vector3 spawn, ProvingArena supportArena=null)
        {
            if (provingProfile == null) throw new System.ArgumentNullException(nameof(provingProfile));
            if (provingProfile.Validate().Count != 0) throw new System.ArgumentException("Proving profile is invalid.", nameof(provingProfile));

            profile = provingProfile;arena=supportArena;
            controller = GetComponent<CharacterController>();
            // Auto Sync Transforms is disabled by project settings. Recreate the native controller
            // at its requested feet anchor, never at the temporary AddComponent origin.
            controller.enabled = false;
            controller.radius = profile.Get("player.capsule.radius");
            controller.height = profile.Get("player.capsule.height");
            controller.center = Vector3.up * (controller.height * .5f);
            controller.skinWidth = profile.Get("player.capsule.skinWidth");
            controller.stepOffset = profile.Get("player.movement.stepOffset");
            controller.slopeLimit = profile.Get("player.movement.slopeLimitDegrees");
            transform.SetPositionAndRotation(spawn, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            state = new ParticipantState { Position = spawn, Velocity = Vector3.zero, Yaw = 0f, Pitch = 0f, Grounded = controller.isGrounded };
            initialized = true;
        }

        public void ValidateState(ParticipantState value)
        {
            foreach(var number in new[]{value.Position.x,value.Position.y,value.Position.z,value.Velocity.x,value.Velocity.y,value.Velocity.z,value.Yaw,value.Pitch,value.LookNeutralSeconds,value.LookReturnVelocity})
                if(float.IsNaN(number)||float.IsInfinity(number))throw new System.ArgumentException("Invalid participant pose");
            if(Mathf.Abs(value.Pitch)>profile.Get("camera.maximumPitchDegrees")||value.LookNeutralSeconds<0)throw new System.ArgumentException("Invalid participant look state");
        }
        public void RestoreState(ParticipantState value)
        {
            ValidateState(value);bool enabled=controller.enabled;controller.enabled=false;
            transform.SetPositionAndRotation(value.Position,Quaternion.Euler(0,value.Yaw,0));
            controller.enabled=enabled;state=value;Physics.SyncTransforms();
        }
        public void SetAlive(bool alive)
        {
            controller.enabled = alive;
            if (!alive) state.Velocity = Vector3.zero;
        }
        public void SetHorizontalSpeedMultiplier(float multiplier)
        {
            if(float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<=0)throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            horizontalSpeedMultiplier=multiplier;
        }

        public void Tick(LocalAction action, float dt)
        {
            AcceptedJumpThisTick=false;
            if (!initialized) throw new System.InvalidOperationException("Initialize must be called before Tick.");
            if (dt <= 0f) throw new System.ArgumentOutOfRangeException(nameof(dt));

            if (!controller.enabled) return;
            state.Yaw = Mathf.Repeat(state.Yaw + action.LookDegrees.x, 360f);
            var maximumPitch = profile.Get("camera.maximumPitchDegrees");
            if(!action.GamepadLookLocked)
                state.Pitch = Mathf.Clamp(state.Pitch - action.LookDegrees.y, -maximumPitch, maximumPitch);
            if(action.ResetLookPitch)state.Pitch=0f;
            transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);

            var groundedBeforeMove = controller.isGrounded;
            var moveInput = Vector2.ClampMagnitude(action.Move, 1f);
            var desiredDirection = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
            desiredDirection.y = 0f;
            var desiredHorizontalVelocity = desiredDirection * profile.Get("player.movement.maximumGroundSpeed")*horizontalSpeedMultiplier;
            // Native penetration resolution may displace a capsule faster than its movement limit.
            // Do not feed that correction back as locomotion momentum on subsequent ticks.
            var horizontalVelocity = Vector3.ClampMagnitude(new Vector3(state.Velocity.x, 0f, state.Velocity.z),
                profile.Get("player.movement.maximumGroundSpeed")*horizontalSpeedMultiplier);
            var acceleration = groundedBeforeMove
                ? (moveInput.sqrMagnitude > 0f ? profile.Get("player.movement.groundAcceleration") : profile.Get("player.movement.groundDeceleration"))
                : profile.Get("player.movement.airAcceleration");
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredHorizontalVelocity, acceleration * dt);

            var verticalVelocity = state.Velocity.y;
            if (groundedBeforeMove)
            {
                verticalVelocity = -profile.Get("player.movement.gravity") * dt;
                if (action.Jump) verticalVelocity = profile.Get("player.movement.jumpSpeed");
                if (action.Jump) AcceptedJumpThisTick=true;
            }
            else
            {
                verticalVelocity -= profile.Get("player.movement.gravity") * dt;
            }

            var previousPosition = transform.position;
            var collisionFlags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            var actualVelocity = (transform.position - previousPosition) / dt;
            if ((collisionFlags & CollisionFlags.Above) != 0 && actualVelocity.y > 0f) actualVelocity.y = 0f;
            state.Position = transform.position;
            state.Velocity = actualVelocity;
            state.Grounded = controller.isGrounded || (collisionFlags & CollisionFlags.Below) != 0;
            if(action.GamepadLookLocked||action.ResetLookPitch)
            {state.LookNeutralSeconds=0;state.LookReturnVelocity=0;return;}
            ApplyLookAssistance(action,dt);

        }
        void ApplyLookAssistance(LocalAction action,float dt)
        {
            if(!action.GamepadLookAssistance||action.ManualLook||action.LookDegrees.sqrMagnitude>0f||!state.Grounded||action.Jump)
            {state.LookNeutralSeconds=0;state.LookReturnVelocity=0;return;}
            state.LookNeutralSeconds+=dt;
            if(state.LookNeutralSeconds<=profile.Get("input.gamepadReturnDelay"))return;
            var origin=state.Position+Vector3.up*profile.Get("player.movement.stepOffset");
            // Capsule contact on an incline can sit above the centre-foot plane by its radius.
            var distance=profile.Get("player.movement.stepOffset")+profile.Get("player.capsule.radius")+profile.Get("player.capsule.skinWidth")*2;
            if(!gameObject.scene.GetPhysicsScene().Raycast(origin,Vector3.down,out var hit,distance,
                (1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))return;
            var forward=Quaternion.Euler(0,state.Yaw,0)*Vector3.forward;
            float target=arena?arena.SupportPitch(hit.collider,hit.normal,forward):SurfacePitch(hit.normal,forward);
            target=Mathf.Clamp(target,-profile.Get("camera.maximumPitchDegrees"),profile.Get("camera.maximumPitchDegrees"));
            state.Pitch=Mathf.SmoothDamp(state.Pitch,target,ref state.LookReturnVelocity,
                profile.Get("input.gamepadReturnSmoothingSeconds"),profile.Get("input.gamepadReturnDegreesPerSecond"),dt);
        }
        public static float SurfacePitch(Vector3 normal,Vector3 forward)
        {
            // Reject walls and vertical normals: they cannot be grounded walkable support.
            if(normal.y<=0)return 0;
            var gradient=new Vector3(-normal.x/normal.y,0,-normal.z/normal.y);
            return -Mathf.Atan(Vector3.Dot(gradient,forward))*Mathf.Rad2Deg;
        }
    }
}
