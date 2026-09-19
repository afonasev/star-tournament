using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable]
    public sealed class NumericDescriptor
    {
        public string Path;
        public string Group;
        public string Label;
        public string Description;
        public string Unit;
        public float Minimum;
        public float Maximum;
        public float Step;
        public float DefaultValue;

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Path)) { reason = "Path is required."; return false; }
            if (string.IsNullOrWhiteSpace(Group)) { reason = "Group is required."; return false; }
            if (string.IsNullOrWhiteSpace(Label)) { reason = "Label is required."; return false; }
            if (string.IsNullOrWhiteSpace(Description)) { reason = "Description is required."; return false; }
            if (string.IsNullOrWhiteSpace(Unit)) { reason = "Unit is required."; return false; }
            if (!IsFinite(Minimum) || !IsFinite(Maximum) || Minimum > Maximum) { reason = "Minimum must be finite and no greater than Maximum."; return false; }
            if (!IsFinite(Step) || Step <= 0f) { reason = "Step must be finite and positive."; return false; }
            if (!IsFinite(DefaultValue) || DefaultValue < Minimum || DefaultValue > Maximum) { reason = "DefaultValue is outside the descriptor range."; return false; }
            reason = string.Empty;
            return true;
        }

        public bool Contains(float value) => IsFinite(value) && value >= Minimum && value <= Maximum;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class ProvingProfileValue
    {
        public string Path;
        public float Value;
    }

    [Serializable]
    public sealed class ProfileValidationIssue
    {
        public string Path;
        public string Message;
    }

    /// <summary>
    /// Single numeric registry for the Unity proving ground. It is deliberately data-only:
    /// input, cameras, fixtures, and motors can share its ranges without sharing runtime objects.
    /// </summary>
    [Serializable]
    public sealed class ProvingProfile
    {
        public const string DefaultId = "unity-proving-ground-v1";
        public const int DefaultVersion = 1;

        [SerializeField] private string id = DefaultId;
        [SerializeField] private int version = DefaultVersion;
        [SerializeField] private List<NumericDescriptor> descriptors = new List<NumericDescriptor>();
        [SerializeField] private List<ProvingProfileValue> values = new List<ProvingProfileValue>();

        public string Id => id;
        public int Version => version;
        public IReadOnlyList<NumericDescriptor> Descriptors => descriptors;

        public float Get(string path)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index].Path == path) return values[index].Value;
            throw new KeyNotFoundException($"Unknown proving profile path '{path}'.");
        }

        /// <summary>Sets an authored value; call Validate before using a changed profile.</summary>
        public void Set(string path, float value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].Path != path) continue;
                values[index].Value = value;
                return;
            }
            throw new KeyNotFoundException($"Unknown proving profile path '{path}'.");
        }

        public IReadOnlyList<ProfileValidationIssue> Validate()
        {
            var issues = new List<ProfileValidationIssue>();
            var knownPaths = new HashSet<string>();
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor == null)
                {
                    issues.Add(new ProfileValidationIssue { Path = $"descriptors[{index}]", Message = "Descriptor is required." });
                    continue;
                }
                if (!descriptor.Validate(out var reason))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = reason });
                if (!knownPaths.Add(descriptor.Path))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = "Descriptor path must be unique." });
            }

            var valuePaths = new HashSet<string>();
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (value == null)
                {
                    issues.Add(new ProfileValidationIssue { Path = $"values[{index}]", Message = "Profile value is required." });
                    continue;
                }
                if (!valuePaths.Add(value.Path))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Profile value path must be unique." });
                var descriptor = FindDescriptor(value.Path);
                if (descriptor == null)
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Value has no descriptor." });
                else if (descriptor.Unit == "ammo" && value.Value != Mathf.Round(value.Value))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Ammo must be integral." });
                else if (!descriptor.Contains(value.Value))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = $"Value {value.Value} is outside [{descriptor.Minimum}, {descriptor.Maximum}]." });
            }
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor != null && !valuePaths.Contains(descriptor.Path))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = "Descriptor has no profile value." });
            }
            return issues;
        }

        /// <summary>Domain-only combat tuning; not applied to the diagnostic scene.</summary>
        public static ProvingProfile CreateCombatDefault()
        {
            var profile = new ProvingProfile { id = "unity-combat-state-v1", version = 1 };
            profile.Add("combat.maximumHealth", "combat", "Здоровье", "Полное здоровье каждой новой жизни.", "health", 1, 1000, 1, 100);
            profile.Add("combat.startingAmmo", "shotgun", "Стартовый боезапас", "Количество выстрелов в начале жизни.", "ammo", 1, 200, 1, 20);
            profile.Add("combat.emptyRefillAmmo", "shotgun", "Пополнение при нуле", "Немедленный запас после последнего выстрела без сброса cooldown.", "ammo", 1, 100, 1, 20);
            profile.Add("combat.cooldownSeconds", "shotgun", "Интервал выстрелов", "Минимальное simulation время до следующего выстрела.", "seconds", .05f, 5, .05f, .7f);
            profile.Add("combat.killcamSeconds", "death", "Ожидание возрождения", "Simulation время смерти до готовности к выбору spawn.", "seconds", 0, 15, .1f, 3);
            return profile;
        }

        public static ProvingProfile CreateDefault()
        {
            var profile = new ProvingProfile();
            profile.Add("simulation.fixedTickHz", "simulation", "Fixed tick frequency", "Frequency of native movement and gameplay ticks, independent of rendered FPS.", "Hz", 30f, 120f, 1f, 50f);
            profile.Add("player.capsule.radius", "player-capsule", "Capsule radius", "Collision radius of a participant capsule.", "meters", .2f, 1f, .01f, .55f);
            profile.Add("player.capsule.height", "player-capsule", "Capsule height", "Full CharacterController capsule height.", "meters", 1f, 4f, .01f, 1.8f);
            profile.Add("player.capsule.skinWidth", "player-capsule", "Controller skin width", "Minimum collision separation used by CharacterController.", "meters", .001f, .1f, .001f, .01f);
            profile.Add("player.movement.maximumGroundSpeed", "player-movement", "Maximum ground speed", "Horizontal speed cap on ground and in air.", "meters-per-second", 1f, 30f, .1f, 9f);
            profile.Add("player.movement.groundAcceleration", "player-movement", "Ground acceleration", "Horizontal acceleration while grounded.", "meters-per-second-squared", .1f, 200f, .1f, 45f);
            profile.Add("player.movement.groundDeceleration", "player-movement", "Ground deceleration", "Horizontal deceleration without directional input.", "meters-per-second-squared", .1f, 200f, .1f, 32f);
            profile.Add("player.movement.airAcceleration", "player-movement", "Air acceleration", "Horizontal steering acceleration while airborne.", "meters-per-second-squared", .1f, 100f, .1f, 12f);
            profile.Add("player.movement.gravity", "player-movement", "Gravity", "Positive downward acceleration.", "meters-per-second-squared", .1f, 100f, .1f, 24f);
            profile.Add("player.movement.jumpSpeed", "player-movement", "Jump speed", "Vertical speed applied to a grounded jump.", "meters-per-second", .1f, 30f, .1f, 8.5f);
            profile.Add("player.movement.stepOffset", "player-movement", "Step offset", "Highest stair step traversable by CharacterController without jumping.", "meters", 0f, 1f, .01f, .45f);
            profile.Add("player.movement.slopeLimitDegrees", "player-movement", "Slope limit", "Maximum climbable CharacterController slope angle.", "degrees", 0f, 89f, 1f, 45f);
            profile.Add("input.mouseDegreesPerPixel", "input", "Mouse degrees per pixel", "Look rotation created by one mouse pixel.", "degrees-per-pixel", .01f, 2f, .01f, .12f);
            profile.Add("input.gamepadDegreesPerSecond", "input", "Gamepad degrees per second", "Maximum analog-stick look rotation rate.", "degrees-per-second", 1f, 720f, 1f, 180f);
            profile.Add("input.deadzone", "input", "Input deadzone", "Radial deadzone applied before creating LocalAction.", "ratio", 0f, .95f, .01f, .15f);
            profile.Add("camera.eyeHeight", "camera", "Eye height", "Camera height above the participant feet anchor.", "meters", .1f, 2.5f, .01f, 1.55f);
            profile.Add("camera.maximumPitchDegrees", "camera", "Maximum pitch", "Symmetric vertical aim clamp.", "degrees", 30f, 89f, 1f, 85f);
            profile.Add("camera.fieldOfViewDegrees", "camera", "Field of view", "Vertical camera field of view.", "degrees", 50f, 130f, 1f, 90f);
            profile.Add("camera.nearClipPlane", "camera", "Near clip plane", "Nearest camera rendering distance.", "meters", .01f, 10f, .01f, .05f);
            profile.Add("camera.farClipPlane", "camera", "Far clip plane", "Farthest camera rendering distance.", "meters", 10f, 1000f, 1f, 250f);
            profile.Add("camera.stairSmoothingSeconds", "camera", "Stair smoothing", "Camera-only vertical smoothing duration for stair traversal.", "seconds", 0f, 1f, .01f, .12f);
            profile.Add("camera.stairMaximumOffsetMeters", "camera", "Maximum stair smoothing offset", "Maximum camera-only vertical smoothing offset.", "meters", 0f, 1f, .01f, .2f);
            // Palette and layer identities are technical fixture constants, not balance controls.
            profile.Add("ui.fontSize", "ui", "Body font size", "Base font size for four-seat HUD labels.", "points", 12f, 48f, 1f, 20f);
            profile.Add("ui.headingFontSize", "ui", "Heading font size", "Heading font size for setup and pause UI.", "points", 16f, 64f, 1f, 28f);
            profile.Add("presentation.lightIntensity", "presentation", "Fixture light intensity", "Presentation-only fixture light intensity.", "intensity", .1f, 4f, .1f, 1.1f);
            profile.Add("presentation.viewWeaponX", "presentation", "View weapon X", "First-person weapon mount horizontal translation from camera origin.", "meters", -2f, 2f, .01f, .36f);
            profile.Add("presentation.viewWeaponY", "presentation", "View weapon Y", "First-person weapon mount vertical translation from camera origin.", "meters", -2f, 2f, .01f, -.34f);
            profile.Add("presentation.viewWeaponZ", "presentation", "View weapon Z", "First-person weapon mount forward translation from camera origin.", "meters", -2f, 2f, .01f, .68f);
            profile.Add("presentation.worldWeaponMountY", "presentation", "World weapon mount Y", "World weapon mount translation above the robot semantic hand mount.", "meters", -1f, 1f, .01f, .22f);
            profile.Add("navigation.sampleDistance", "navigation", "Navigation sample distance", "AI Navigation sampling spacing for the proving-ground bake.", "meters", .1f, 10f, .1f, 2f);
            profile.Add("fixture.width", "fixture", "Fixture width", "Overall fixed proving-ground arena width.", "meters", 12f, 80f, 1f, 32f);
            profile.Add("fixture.depth", "fixture", "Fixture depth", "Overall fixed proving-ground arena depth.", "meters", 12f, 80f, 1f, 32f);
            profile.Add("fixture.wallThickness", "fixture", "Wall thickness", "Physical fixture wall thickness.", "meters", .1f, 2f, .05f, .3f);
            profile.Add("fixture.wallHeight", "fixture", "Wall height", "Physical fixture wall height.", "meters", 1.5f, 8f, .1f, 3f);
            profile.Add("fixture.floorThickness", "fixture", "Floor thickness", "Physical floor and upper slab thickness.", "meters", .1f, 2f, .05f, .3f);
            profile.Add("fixture.floorHeight", "fixture", "Lower floor height", "World height of the lower walkable floor.", "meters", -5f, 5f, .1f, 0f);
            profile.Add("fixture.upperFloorHeight", "fixture", "Upper floor height", "World height of the upper walkable floor.", "meters", 2f, 10f, .1f, 4f);
            profile.Add("fixture.stairRise", "fixture", "Stair rise", "Physical rise of one fixture stair.", "meters", .05f, .5f, .01f, .25f);
            profile.Add("fixture.stairTread", "fixture", "Stair tread", "Physical depth of one fixture stair.", "meters", .1f, 1f, .01f, .75f);
            profile.Add("fixture.rampLength", "fixture", "Ramp length", "Horizontal run of the fixture ramp.", "meters", 3f, 20f, .5f, 12f);
            profile.Add("fixture.rampWidth", "fixture", "Ramp width", "Clear width of the fixture ramp.", "meters", 1.2f, 8f, .1f, 4f);
            profile.Add("fixture.headroomHeight", "fixture", "Headroom height", "Actual lower-route clearance under the fixture upper slab.", "meters", 1f, 4f, .05f, 2.1f);
            profile.Add("weapon.probeRange", "weapon", "Weapon probe range", "Fixture hitscan diagnostic range.", "meters", 1f, 100f, 1f, 35f);
            profile.Add("weapon.probeCooldown", "weapon", "Weapon probe cooldown", "Minimum interval between fixture firing probes.", "seconds", .1f, 3f, .05f, .7f);
            return profile;
        }

        private void Add(string path, string group, string label, string description, string unit, float minimum, float maximum, float step, float defaultValue)
        {
            descriptors.Add(new NumericDescriptor { Path = path, Group = group, Label = label, Description = description, Unit = unit, Minimum = minimum, Maximum = maximum, Step = step, DefaultValue = defaultValue });
            values.Add(new ProvingProfileValue { Path = path, Value = defaultValue });
        }

        private NumericDescriptor FindDescriptor(string path)
        {
            for (var index = 0; index < descriptors.Count; index++)
                if (descriptors[index] != null && descriptors[index].Path == path) return descriptors[index];
            return null;
        }
    }
}
