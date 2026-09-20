using UnityEngine;

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
        private ParticipantState state;
        private bool initialized;

        public ParticipantState State => state;

        public void Initialize(ProvingProfile provingProfile, Vector3 spawn)
        {
            if (provingProfile == null) throw new System.ArgumentNullException(nameof(provingProfile));
            if (provingProfile.Validate().Count != 0) throw new System.ArgumentException("Proving profile is invalid.", nameof(provingProfile));

            profile = provingProfile;
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

        public void SetAlive(bool alive)
        {
            controller.enabled = alive;
            if (!alive) state.Velocity = Vector3.zero;
        }

        public void Tick(LocalAction action, float dt)
        {
            if (!initialized) throw new System.InvalidOperationException("Initialize must be called before Tick.");
            if (dt <= 0f) throw new System.ArgumentOutOfRangeException(nameof(dt));

            if (!controller.enabled) return;
            state.Yaw = Mathf.Repeat(state.Yaw + action.LookDegrees.x, 360f);
            var maximumPitch = profile.Get("camera.maximumPitchDegrees");
            state.Pitch = Mathf.Clamp(state.Pitch - action.LookDegrees.y, -maximumPitch, maximumPitch);
            transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);

            var groundedBeforeMove = controller.isGrounded;
            var moveInput = Vector2.ClampMagnitude(action.Move, 1f);
            var desiredDirection = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
            desiredDirection.y = 0f;
            var desiredHorizontalVelocity = desiredDirection * profile.Get("player.movement.maximumGroundSpeed");
            var horizontalVelocity = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
            var acceleration = groundedBeforeMove
                ? (moveInput.sqrMagnitude > 0f ? profile.Get("player.movement.groundAcceleration") : profile.Get("player.movement.groundDeceleration"))
                : profile.Get("player.movement.airAcceleration");
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredHorizontalVelocity, acceleration * dt);

            var verticalVelocity = state.Velocity.y;
            if (groundedBeforeMove)
            {
                verticalVelocity = -profile.Get("player.movement.gravity") * dt;
                if (action.Jump) verticalVelocity = profile.Get("player.movement.jumpSpeed");
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
        }
    }
}
