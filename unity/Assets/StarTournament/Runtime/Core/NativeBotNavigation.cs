using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct NativeNavigationPoint
    {
        public Vector3 Position;
        public string Support;
        public NativeNavigationPoint(Vector3 position, string support) { Position = position; Support = support; }
    }
    public interface INativeNavigation
    {
        string Identity { get; }
        bool TryLocate(Vector3 feet, out NativeNavigationPoint point);
        bool ValidTransition(string transition, string exitSupport);
        bool TryRoute(Vector3 from, Vector3 goal, out NativeNavigationPoint[] points, out string failure);
    }
    public enum NativeNavigationStatus { Idle, Moving, Recovering, Arrived, Blocked }
    [Serializable] public sealed class NativeNavigationState
    {
        public int Version = 1, OwnLife, Enemy = -1, EnemyLife, Cursor, Recoveries, Replans;
        public string Configuration, Failure, ActiveTransition, ExitSupport;
        public bool HasGoal;
        public Vector3 Goal;
        public Vector3 TransitionExit;
        public double ObservedAt = -1, Time = -1, NextRepath, ProgressAt, RecoveryUntil;
        public float BestRemaining;
        public NativeNavigationStatus Status;
        public NativeNavigationPoint[] Route = Array.Empty<NativeNavigationPoint>();
    }

    /// <summary>Own pose and filtered knowledge only. Produces commands; never owns a motor or raw session.</summary>
    public sealed class NativeBotNavigation
    {
        readonly INativeNavigation navigation;
        readonly ProvingProfile profile;
        readonly string configuration;
        NativeNavigationState state;
        bool routeDirty;
        float P(string suffix) => profile.Get("bots.navigation." + suffix);
        public NativeNavigationStatus Status => state.Status;
        public bool InTransition => !string.IsNullOrEmpty(state.ActiveTransition);
        public int FollowedEnemy => state.Enemy;
        public NativeNavigationState Capture() => Copy(state);
        static NativeNavigationState Copy(NativeNavigationState value) => JsonUtility.FromJson<NativeNavigationState>(JsonUtility.ToJson(value));

        public NativeBotNavigation(INativeNavigation navigation, ProvingProfile profile)
        {
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            var canonical = ProvingProfile.CreateNavigationDefault();
            if (profile == null || profile.Id != canonical.Id || profile.Version != canonical.Version || profile.Validate().Count != 0)
                throw new ArgumentException("Invalid navigation profile");
            foreach (var d in canonical.Descriptors)
                if (!d.Contains(profile.Get(d.Path))) throw new ArgumentException("Invalid navigation value: " + d.Path);
            if (profile.Get("bots.navigation.maximumRecoveries") % 1 != 0) throw new ArgumentException("Recovery count must be integral");
            this.profile = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile));
            configuration = navigation.Identity + "|" + JsonUtility.ToJson(this.profile);
            state = new NativeNavigationState { Configuration = configuration };
        }
        public void SetStaticGoal(Vector3 goal)
        {
            if (!Finite(goal)) throw new ArgumentException("Invalid goal");
            if (state.Status == NativeNavigationStatus.Blocked || state.Status == NativeNavigationStatus.Arrived)
            {
                state.Recoveries = 0; state.Status = NativeNavigationStatus.Moving;
                state.Route = Array.Empty<NativeNavigationPoint>(); state.Cursor = 0;
                state.ProgressAt = state.Time;
                routeDirty = true;
            }
            SetGoal(goal, -1, 0, -1);
        }
        public void FollowEnemy(int participant)
        {
            if (participant < 0) throw new ArgumentOutOfRangeException(nameof(participant));
            if (InTransition) { state.Enemy=participant; return; }
            Clear(); state.Enemy = participant;
        }
        void SetGoal(Vector3 goal, int enemy, int life, double observed)
        {
            bool changed = !state.HasGoal || state.Goal != goal || state.Enemy != enemy || state.EnemyLife != life;
            state.HasGoal = true; state.Goal = goal; state.Enemy = enemy; state.EnemyLife = life; state.ObservedAt = observed;
            if (changed)
            {
                if (state.Status == NativeNavigationStatus.Arrived)
                {
                    state.Route = Array.Empty<NativeNavigationPoint>(); state.Cursor = 0;
                    state.ProgressAt = state.Time; state.Recoveries = 0;
                }
                routeDirty = true;
                if (state.Status == NativeNavigationStatus.Idle || state.Status == NativeNavigationStatus.Arrived) state.Status = NativeNavigationStatus.Moving;
            }
        }
        public void RetryBlockedRoute()
        {
            if(state.Status!=NativeNavigationStatus.Blocked||!state.HasGoal)return;
            state.Status=NativeNavigationStatus.Moving;state.Recoveries=0;state.ProgressAt=state.Time;
            state.Route=Array.Empty<NativeNavigationPoint>();state.Cursor=0;state.NextRepath=0;routeDirty=true;
            // Active transition/exit survives retries; a new path still targets that exit first.
        }
        public void ForgetEnemy()
        {
            if(!InTransition){Clear();return;}
            state.Enemy=-1;state.EnemyLife=0;state.ObservedAt=-1;
            state.HasGoal=true;state.Goal=state.TransitionExit;routeDirty=true;
        }
        public void Clear()
        {
            state.HasGoal = false; state.Enemy = -1; state.EnemyLife = 0; state.ObservedAt = -1;
            state.Route = Array.Empty<NativeNavigationPoint>(); state.Cursor = 0; state.Status = NativeNavigationStatus.Idle;
            state.Recoveries = 0; state.Failure = null; routeDirty = false;
            // A cleared route has no pending repath throttle; the next goal must plan immediately.
            state.NextRepath = 0;
            state.ActiveTransition = null; state.ExitSupport = null;
        }
        public void Restore(NativeNavigationState snapshot)
        {
            if (snapshot == null || snapshot.Version != 1 || snapshot.Configuration != configuration || snapshot.OwnLife < 0 ||
                snapshot.Enemy < -1 || snapshot.EnemyLife < 0 || snapshot.Cursor < 0 || snapshot.Route == null || snapshot.Cursor > snapshot.Route.Length ||
                snapshot.Recoveries < 0 || snapshot.Recoveries > P("maximumRecoveries") || snapshot.Replans < 0 ||
                !Enum.IsDefined(typeof(NativeNavigationStatus), snapshot.Status) || !Finite(snapshot.Goal) || !Finite(snapshot.TransitionExit) ||
                !Finite(snapshot.Time) || snapshot.Time < -1 || !Finite(snapshot.ObservedAt) || snapshot.ObservedAt < -1 ||
                !Finite(snapshot.NextRepath) || !Finite(snapshot.ProgressAt) || !Finite(snapshot.RecoveryUntil) ||
                !Finite(snapshot.BestRemaining) || snapshot.BestRemaining < 0)
                throw new ArgumentException("Invalid navigation snapshot");
            foreach (var point in snapshot.Route)
                if (!Finite(point.Position) || string.IsNullOrEmpty(point.Support)) throw new ArgumentException("Invalid route point");
            bool active = !string.IsNullOrEmpty(snapshot.ActiveTransition), exit = !string.IsNullOrEmpty(snapshot.ExitSupport);
            if (active != exit || (active && (!snapshot.HasGoal || !navigation.ValidTransition(snapshot.ActiveTransition, snapshot.ExitSupport))) ||
                (!snapshot.HasGoal && snapshot.Status != NativeNavigationStatus.Idle) ||
                (snapshot.Status == NativeNavigationStatus.Recovering && snapshot.RecoveryUntil < snapshot.Time))
                throw new ArgumentException("Inconsistent navigation state");
            state = Copy(snapshot); routeDirty = state.HasGoal;
            // Native routes are revalidated, not restored as authoritative physics handles.
            state.NextRepath = 0;
        }
        public LocalAction Tick(double time, ParticipantState pose, NativeBotKnowledge knowledge, float? aimYaw = null, bool tacticalControl = false)
        {
            if (!Finite(time) || time < 0 || time <= state.Time || !Finite(pose.Position) || !Finite(pose.Yaw) ||
                knowledge == null || knowledge.OwnLife < 1 || knowledge.Enemies == null || (aimYaw.HasValue && !Finite(aimYaw.Value)))
                throw new ArgumentException("Invalid navigation tick");
            state.Time = time;
            if (!knowledge.Alive || (state.OwnLife != 0 && state.OwnLife != knowledge.OwnLife)) Clear();
            state.OwnLife = knowledge.OwnLife;
            if (!knowledge.Alive) return default;
            if (state.Enemy >= 0)
            {
                bool found = false;
                foreach (var memory in knowledge.Enemies)
                    if (memory.Sighting.Participant == state.Enemy)
                    {
                        SetGoal(memory.Sighting.Position, state.Enemy, memory.Sighting.Life, memory.ObservedAt); found = true; break;
                    }
                if (!found) { Clear(); return default; }
            }
            if (!state.HasGoal || state.Status == NativeNavigationStatus.Blocked) return default;
            // Airborne tactical input follows a prevalidated motor trajectory; do not replan from mid-air feet.
            if(tacticalControl && !pose.Grounded && !InTransition){state.ProgressAt=time;return default;}
            bool located = navigation.TryLocate(pose.Position, out var location);
            if (!string.IsNullOrEmpty(state.ActiveTransition) && located && location.Support == state.ExitSupport && pose.Grounded)
            { state.ActiveTransition = null; state.ExitSupport = null; routeDirty = true; }
            if (string.IsNullOrEmpty(state.ActiveTransition) && located && location.Support.StartsWith("transition:", StringComparison.Ordinal))
            {
                for (int i = state.Cursor; i < state.Route.Length; i++)
                    if (!state.Route[i].Support.StartsWith("transition:", StringComparison.Ordinal))
                    { state.ActiveTransition = location.Support; state.TransitionExit = state.Route[i].Position; state.ExitSupport = state.Route[i].Support; break; }
            }
            if (state.Status == NativeNavigationStatus.Recovering)
            {
                if (time < state.RecoveryUntil)
                    return new LocalAction { Move = new Vector2(state.Recoveries % 2 == 1 ? 1 : -1, -1).normalized };
                routeDirty = true; state.Status = NativeNavigationStatus.Moving; state.ProgressAt = time;
            }
            if (routeDirty && time >= state.NextRepath && (string.IsNullOrEmpty(state.ActiveTransition) || state.NextRepath == 0))
            {
                bool firstRoute = state.Route.Length == 0;
                state.NextRepath = time + P("repathSeconds"); state.Replans++;
                var destination = string.IsNullOrEmpty(state.ActiveTransition) ? state.Goal : state.TransitionExit;
                if (!navigation.TryRoute(pose.Position, destination, out var route, out var failure) || route == null || route.Length == 0)
                { state.Status = NativeNavigationStatus.Blocked; state.Failure = failure; return default; }
                state.Route = (NativeNavigationPoint[])route.Clone(); state.Cursor = 0; routeDirty = false;
                state.BestRemaining = Remaining(pose.Position); if (firstRoute) state.ProgressAt = time;
            }
            if (state.Route.Length == 0) return default;
            while (state.Cursor < state.Route.Length)
            {
                var delta = state.Route[state.Cursor].Position - pose.Position;
                bool onSupport = located && location.Support == state.Route[state.Cursor].Support;
                // Crossing a declared boundary can place the capsule on the next support before its centre
                // reaches the old corner; only the next ordered support is acceptable, never an arbitrary floor.
                if (!onSupport && located && state.Cursor + 1 < state.Route.Length)
                    onSupport = location.Support == state.Route[state.Cursor + 1].Support;
                if (new Vector2(delta.x, delta.z).magnitude > P("waypointRadius") || Mathf.Abs(delta.y) > P("heightTolerance") ||
                    !onSupport || !pose.Grounded) break;
                state.Cursor++;
            }
            if (state.Cursor == state.Route.Length)
            { state.Status = routeDirty ? NativeNavigationStatus.Moving : NativeNavigationStatus.Arrived; return default; }
            var remaining = Remaining(pose.Position);
            // A deliberate combat manoeuvre owns motion; it is not a failed route attempt.
            if (tacticalControl && !InTransition) { state.ProgressAt = time; state.BestRemaining = remaining; }
            if (remaining <= state.BestRemaining - P("progressMeters"))
            { state.BestRemaining = remaining; state.ProgressAt = time; }
            if (time - state.ProgressAt >= P("stuckSeconds"))
            {
                if (state.Recoveries >= P("maximumRecoveries"))
                { state.Status = NativeNavigationStatus.Blocked; state.Failure = "No physical route progress"; return default; }
                state.Recoveries++; state.Status = NativeNavigationStatus.Recovering;
                state.RecoveryUntil = time + P("recoverySeconds"); return default;
            }
            state.Status = NativeNavigationStatus.Moving;
            var direction = state.Route[state.Cursor].Position - pose.Position; direction.y = 0;
            float yaw = aimYaw ?? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var local = Quaternion.Euler(0, -yaw, 0) * direction.normalized;
            return new LocalAction
            {
                // Intermediate samples describe support, not stopping points: do not brake at every stair.
                Move = new Vector2(local.x, local.z) * (state.Cursor == state.Route.Length - 1 ? Mathf.Clamp01(direction.magnitude / P("slowDistance")) : 1),
                LookDegrees = new Vector2(Mathf.DeltaAngle(pose.Yaw, yaw), 0)
            };
        }
        float Remaining(Vector3 position)
        {
            if (state.Cursor >= state.Route.Length) return 0;
            float length = Vector3.Distance(position, state.Route[state.Cursor].Position);
            for (int i = state.Cursor + 1; i < state.Route.Length; i++) length += Vector3.Distance(state.Route[i - 1].Position, state.Route[i].Position);
            return length;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
