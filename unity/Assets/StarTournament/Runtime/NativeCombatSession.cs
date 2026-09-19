using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public readonly struct DeathNotice
    {
        public readonly int Seat;
        public readonly CombatLifeState Life;
        public readonly ParticipantState Pose;
        public DeathNotice(int seat, CombatLifeState life, ParticipantState pose) { Seat=seat; Life=life; Pose=pose; }
    }
    /// <summary>Scene adapter. Only the owner calls Tick while Running. Core remains renderer/device-free.</summary>
    public sealed class NativeCombatSession
    {
        readonly CharacterMotor[] motors;
        readonly CombatLife[] lives;
        readonly PhysicsScene physics;
        readonly ShotgunResolver resolver;
        readonly ProvingProfile movement;
        readonly bool[] releaseRequired;
        uint shotSequence;
        bool stopped;
        public NativeMatchState Match { get; }
        public void Stop() { stopped=true; ClearInput(); }
        public SafeSpawnSelector Spawns { get; }
        public double Time { get; private set; }
        public int ShotCount { get; private set; }
        public event Action<DeathNotice> Died;
        public event Action<int> Respawned;
        public event Action<int, float> Fired;
        public NativeCombatSession(CharacterMotor[] motors, ProvingArena arena, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile lifecycle, ProvingProfile combat, NativeMatchState match = null)
        {
            this.motors = motors; this.physics = physics; Match=match;
            this.movement = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(movement));
            var tuning = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));
            resolver = new ShotgunResolver(tuning, movement.Get("player.capsule.height"));
            Spawns = new SafeSpawnSelector(physics, arena, this.movement, tuning);
            lives = new CombatLife[motors.Length]; releaseRequired = new bool[motors.Length];
            for (int i = 0; i < lives.Length; i++) lives[i] = new CombatLife("player-"+(i+1), lifecycle);
        }
        public CombatLifeState Life(int seat) => lives[seat].Read();
        public ParticipantState Pose(int seat) => motors[seat].State;
        public CombatTarget[] LiveTargets()
        {
            var result = new List<CombatTarget>();
            for (int i = 0; i < lives.Length; i++) if (!Life(i).Dead) result.Add(new CombatTarget(i, Life(i).Life, Pose(i)));
            return result.ToArray();
        }
        public void ClearInput()
        {
            for (int i = 0; i < lives.Length; i++) { lives[i].ClearHeldInput(); releaseRequired[i] = true; }
        }
        public void Tick(LocalAction[] actions, float seconds)
        {
            if (actions.Length != motors.Length || seconds <= 0 || (float.IsNaN(seconds) || float.IsInfinity(seconds))) throw new ArgumentException("Invalid tick");
            if(stopped || Match?.Phase==NativeMatchPhase.Finished) return;
            Match?.BeginTick();
            Time += seconds;
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i].Advance(seconds);
                if (!Life(i).Dead) motors[i].Tick(actions[i], seconds);
            }
            Physics.SyncTransforms();
            var targets=LiveTargets();
            var shots=new List<(int shooter, int life, float[] damage)>();
            for (int i = 0; i < lives.Length; i++)
            {
                bool held = actions[i].FireHeld || actions[i].Fire;
                if (releaseRequired[i])
                {
                    if (!held) releaseRequired[i] = false;
                    lives[i].Fire(false); continue;
                }
                // A newly captured physical edge also records an intervening release between ticks.
                if (actions[i].Fire) lives[i].ClearHeldInput();
                if (lives[i].Fire(held)) shots.Add((i, Life(i).Life, ResolveShot(i, targets)));
            }
            foreach(var shot in shots)
            {
                float applied=0;
                for(int t=0;t<targets.Length;t++) if(shot.damage[t]>0)
                    applied+=ApplyDamage(targets[t].Seat, targets[t].Life, shot.damage[t]/resolver.PelletCount, shot.shooter, shot.life).Applied;
                Fired?.Invoke(shot.shooter,applied);
            }
            Match?.EndTick();
            if(Match?.Phase==NativeMatchPhase.Finished) return;
            for (int i = 0; i < lives.Length; i++) if (lives[i].ReadyToRespawn)
            {
                if (!Spawns.TryChoose(LiveTargets(), out var spawn))
                    throw new InvalidOperationException("Fixed arena has no physically safe respawn slot");
                if (lives[i].Respawn(Life(i).Life))
                {
                    motors[i].Initialize(movement, spawn);
                    releaseRequired[i] = actions[i].FireHeld || actions[i].Fire;
                    Physics.SyncTransforms(); // Reserve this live capsule before the next participant chooses.
                    Respawned?.Invoke(i);
                }
            }
        }
        float[] ResolveShot(int shooter, CombatTarget[] targets)
        {
            ShotCount++; shotSequence++;
            var damage = new float[targets.Length];
            var pose = Pose(shooter); var aim = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            var origin = pose.Position + Vector3.up*movement.Get("camera.eyeHeight");
            for (int pellet = 0; pellet < resolver.PelletCount; pellet++)
            {
                Vector3 direction = resolver.Direction(aim, pellet, shotSequence);
                float limit = physics.Raycast(origin, direction, out var wall, resolver.Range, 1 << ProvingArena.WorldLayer,
                    QueryTriggerInteraction.Ignore) ? wall.distance : resolver.Range;
                var hit = resolver.Resolve(origin, direction, targets, shooter, limit);
                if (hit.TargetIndex >= 0) damage[hit.TargetIndex] += resolver.FullDamage(hit.Zone);
            }
            return damage;
        }

        public DamageResult ApplyDamage(int seat, int life, float damage, int killer = -1, int killerLife = 0)
        {
            if(stopped || Match?.Phase==NativeMatchPhase.Finished) return default;
            var result = lives[seat].Damage(life, damage, killer < 0 ? null : Life(killer).ParticipantId, killerLife);
            Match?.RecordDamage(seat,killer,result);
            if (result.Killed)
            {
                motors[seat].SetAlive(false);
                releaseRequired[seat] = true;
                Died?.Invoke(new DeathNotice(seat, Life(seat), Pose(seat)));
            }
            return result;
        }
    }
}
