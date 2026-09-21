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
    public enum PelletContact { Miss, World, Participant }
    public readonly struct PelletNotice
    {
        public readonly Vector3 Direction, Endpoint, Normal;
        public readonly PelletContact Contact;
        public readonly int TargetSeat, TargetLife;
        public PelletNotice(Vector3 direction,Vector3 endpoint,Vector3 normal,PelletContact contact,int targetSeat=-1,int targetLife=0)
        { Direction=direction;Endpoint=endpoint;Normal=normal;Contact=contact;TargetSeat=targetSeat;TargetLife=targetLife; }
    }
    public readonly struct ShotNotice
    {
        public readonly uint Sequence; public readonly int Shooter, ShooterLife; public readonly double Time; public readonly Vector3 Origin;
        public readonly PelletNotice[] Pellets;
        public ShotNotice(uint sequence,int shooter,int shooterLife,double time,Vector3 origin,PelletNotice[] pellets)
        { Sequence=sequence;Shooter=shooter;ShooterLife=shooterLife;Time=time;Origin=origin;Pellets=pellets; }
    }
    [Serializable] public sealed class NativeCombatSessionSnapshot { public double Time; public CombatLifeState[] Lives; public ArmorPickupState ArmorPickup; }
    /// <summary>Scene adapter. Only the owner calls Tick while Running. Core remains renderer/device-free.</summary>
    public sealed class NativeCombatSession
    {
        readonly CharacterMotor[] motors;
        readonly CombatLife[] lives;
        readonly PhysicsScene physics;
        readonly ShotgunResolver resolver;
        readonly ProvingProfile movement;
        readonly bool[] releaseRequired;
        readonly bool[] armorPickupWasOutside;
        readonly ArmorPickup armorPickup;
        uint shotSequence;
        bool stopped;
        public NativeMatchState Match { get; }
        public int ParticipantCount => lives.Length;
        public void Stop() { stopped=true; ClearInput(); }
        public SafeSpawnSelector Spawns { get; }
        public double Time { get; private set; }
        public int ShotCount { get; private set; }
        public ArmorPickupState ArmorPickup => armorPickup.Read();
        public NativeCombatSessionSnapshot Capture() => new NativeCombatSessionSnapshot { Time=Time,Lives=Array.ConvertAll(lives,life=>life.Read()),ArmorPickup=armorPickup.Read() };
        public void Restore(NativeCombatSessionSnapshot snapshot)
        {
            if(snapshot==null||double.IsNaN(snapshot.Time)||double.IsInfinity(snapshot.Time)||snapshot.Time<0||snapshot.Lives==null||snapshot.Lives.Length!=lives.Length)throw new ArgumentException("Invalid native combat snapshot",nameof(snapshot));
            for(int i=0;i<lives.Length;i++)lives[i].Restore(snapshot.Lives[i]);armorPickup.Restore(snapshot.ArmorPickup);Time=snapshot.Time;
            for(int i=0;i<lives.Length;i++)armorPickupWasOutside[i]=!armorPickup.InRange(Pose(i).Position);
        }
        public event Action<DeathNotice> Died;
        public event Action<int> Respawned;
        public event Action<int, float> Fired;
        public event Action<ShotNotice> ShotResolved;
        public NativeCombatSession(CharacterMotor[] motors, ProvingArena arena, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile lifecycle, ProvingProfile combat, NativeMatchState match = null)
        {
            if(match != null && match.Roster.Count != motors.Length) throw new ArgumentException("Roster/motor count mismatch");
            this.motors = motors; this.physics = physics; Match=match;
            this.movement = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(movement));
            var tuning = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));
            resolver = new ShotgunResolver(tuning, movement.Get("player.capsule.height"));
            Spawns = new SafeSpawnSelector(physics, arena, this.movement, tuning);
            lives = new CombatLife[motors.Length]; releaseRequired = new bool[motors.Length];armorPickupWasOutside=new bool[motors.Length];
            for (int i = 0; i < lives.Length; i++) lives[i] = new CombatLife("player-"+(i+1), lifecycle);
            armorPickup=new ArmorPickup(arena.Definition.ArmorPickupAnchor,lifecycle);
            for(int i=0;i<motors.Length;i++)armorPickupWasOutside[i]=!armorPickup.InRange(motors[i].State.Position);
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
            armorPickup.Advance(seconds);
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i].Advance(seconds);
                if (!Life(i).Dead) motors[i].Tick(actions[i], seconds);
            }
            Physics.SyncTransforms();
            for(int i=0;i<lives.Length;i++)
            {
                bool outside=!armorPickup.InRange(Pose(i).Position);
                if(armorPickupWasOutside[i]&&!outside) armorPickup.TryCollect(Pose(i).Position,lives[i]);
                armorPickupWasOutside[i]=outside;
            }
            var targets=LiveTargets();
            var shots=new List<(int shooter, int life, ShotNotice notice, float[] damage)>();
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
                if (lives[i].Fire(held))
                {
                    var result=ResolveShot(i,targets);
                    shots.Add((i,Life(i).Life,result.notice,result.damage));
                }
            }
            foreach(var shot in shots)
            {
                float applied=0;
                for(int t=0;t<targets.Length;t++) if(shot.damage[t]>0)
                    applied+=ApplyDamage(targets[t].Seat, targets[t].Life, shot.damage[t]/resolver.PelletCount, shot.shooter, shot.life).Applied;
                ShotResolved?.Invoke(shot.notice);
                Fired?.Invoke(shot.shooter,applied);
            }
            Match?.EndTick();
            if(Match?.Phase==NativeMatchPhase.Finished) return;
            for (int i = 0; i < lives.Length; i++) if (lives[i].ReadyToRespawn)
            {
                if (!Spawns.TryChoose(EnemiesOf(i), out var spawn))
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
        (ShotNotice notice,float[] damage) ResolveShot(int shooter, CombatTarget[] targets)
        {
            ShotCount++; shotSequence++;
            var damage = new float[targets.Length];
            var pose = Pose(shooter); var aim = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            var origin = pose.Position + Vector3.up*movement.Get("camera.eyeHeight");
            var pellets=new PelletNotice[resolver.PelletCount];
            for (int pellet = 0; pellet < resolver.PelletCount; pellet++)
            {
                Vector3 direction = resolver.Direction(aim, pellet, shotSequence);
                bool worldHit=physics.Raycast(origin,direction,out var wall,resolver.Range,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
                float limit=worldHit ? wall.distance : resolver.Range;
                var hit = resolver.Resolve(origin, direction, targets, shooter, limit);
                if(hit.TargetIndex>=0)
                {
                    var target=targets[hit.TargetIndex];
                    pellets[pellet]=new PelletNotice(direction,origin+direction*hit.Distance,-direction,PelletContact.Participant,target.Seat,target.Life);
                    if(!Allied(shooter,target.Seat)) damage[hit.TargetIndex]+=resolver.FullDamage(hit.Zone);
                }
                else if(worldHit) pellets[pellet]=new PelletNotice(direction,wall.point,wall.normal,PelletContact.World);
                else pellets[pellet]=new PelletNotice(direction,origin+direction*resolver.Range,-direction,PelletContact.Miss);
            }
            return (new ShotNotice(shotSequence,shooter,Life(shooter).Life,Time,origin,pellets),damage);
        }

        bool Allied(int a,int b) => Match != null && Match.Roster.AreAllies(a,b);
        CombatTarget[] EnemiesOf(int participant)
        {
            var enemies=new List<CombatTarget>();
            foreach(var target in LiveTargets()) if(!Allied(participant,target.Seat)) enemies.Add(target);
            return enemies.ToArray();
        }
        public DamageResult ApplyDamage(int seat, int life, float damage, int killer = -1, int killerLife = 0)
        {
            if(stopped || Match?.Phase==NativeMatchPhase.Finished) return default;
            if(killer >= 0 && Allied(killer,seat)) return default;
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
