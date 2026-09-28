using System;
using System.Collections.Generic;
using System.Linq;
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
        public readonly WeaponId Weapon;
        public readonly uint Sequence; public readonly int Shooter, ShooterLife; public readonly double Time; public readonly Vector3 Origin;
        public readonly PelletNotice[] Pellets;
        public ShotNotice(uint sequence,int shooter,int shooterLife,double time,Vector3 origin,PelletNotice[] pellets,WeaponId weapon=WeaponId.Shotgun)
        { Sequence=sequence;Shooter=shooter;ShooterLife=shooterLife;Time=time;Origin=origin;Pellets=pellets;Weapon=weapon; }
    }
    [Serializable] public sealed class NativeCombatSessionSnapshot { public int Version=4; public string ArenaIdentity; public LabRevisionReference DesignProfile; public double Time; public uint ShotSequence; public int ShotCount; public CombatLifeState[] Lives; public ArmorPickupState[] ArmorPickups; public SpeedPickupState[] SpeedPickups; public double[] SpeedRemaining; public FullHealPickupState[] HealPickups; public DamageBoostPickupState DamagePickup; public double[] DamageRemaining; }
    /// <summary>Scene adapter. Only the owner calls Tick while Running. Core remains renderer/device-free.</summary>
    public sealed class NativeCombatSession
    {
        readonly CharacterMotor[] motors;
        readonly CombatLife[] lives;
        readonly PhysicsScene physics;
        readonly ShotgunResolver resolver;
        readonly ProvingProfile movement;
        readonly bool[] releaseRequired;
        readonly bool[,] armorPickupWasOutside;
        readonly ArmorPickup[] armorPickups;
        ArmorPickup armorPickup=>armorPickups[0];
        public ArmorPickupState[] ArmorPickups=>Array.ConvertAll(armorPickups,x=>x.Read());
        readonly FullHealPickup[] healPickups;
        public FullHealPickupState[] HealPickups=>Array.ConvertAll(healPickups,x=>x.Read());
        readonly SpeedPickup[] speedPickups;
        SpeedPickup speedPickup=>speedPickups[0];
        public SpeedPickupState[] SpeedPickups=>Array.ConvertAll(speedPickups,x=>x.Read());
        readonly bool[,] speedPickupWasOutside;
        readonly double[] speedRemaining;
        readonly DamageBoostPickup damagePickup; readonly double[] damageRemaining; readonly bool[] damageWasOutside;
        public bool HasDamagePickup { get; } public bool HasSpeedPickup { get; }
        public DamageBoostPickupState DamagePickup => damagePickup.Read();
        public double DamageBoostRemaining(int seat)=>damageRemaining[seat];
        uint shotSequence;
        bool stopped;
        public NativeMatchState Match { get; }
        public string ArenaIdentity { get; }
        readonly LabRevisionReference designProfile;
        public LabRevisionReference DesignProfile=>designProfile?.Clone();
        public int ParticipantCount => lives.Length;
        public void Stop() { stopped=true; ClearInput(); }
        public SafeSpawnSelector Spawns { get; }
        public double Time { get; private set; }
        public int ShotCount { get; private set; }
        public ArmorPickupState ArmorPickup => armorPickup.Read();
        public SpeedPickupState SpeedPickup => speedPickup.Read();
        public NativeCombatSessionSnapshot Capture() => new NativeCombatSessionSnapshot { DesignProfile=DesignProfile,ArenaIdentity=ArenaIdentity,HealPickups=HealPickups,ArmorPickups=ArmorPickups,SpeedPickups=SpeedPickups,Time=Time,ShotSequence=shotSequence,ShotCount=ShotCount,Lives=Array.ConvertAll(lives,life=>life.Read()),SpeedRemaining=(double[])speedRemaining.Clone(),DamagePickup=damagePickup.Read(),DamageRemaining=(double[])damageRemaining.Clone() };
        public void Restore(NativeCombatSessionSnapshot snapshot)
        {
            if(snapshot==null||snapshot.Version!=4||(designProfile!=null&&!designProfile.Matches(snapshot.DesignProfile))||(snapshot.ArenaIdentity!=null&&snapshot.ArenaIdentity!=ArenaIdentity)||double.IsNaN(snapshot.Time)||double.IsInfinity(snapshot.Time)||snapshot.Time<0||snapshot.ShotCount<0||snapshot.Lives==null||snapshot.Lives.Length!=lives.Length||snapshot.SpeedRemaining==null||snapshot.SpeedRemaining.Length!=lives.Length)throw new ArgumentException("Invalid native combat snapshot",nameof(snapshot));
            if(snapshot.ArmorPickups==null||snapshot.SpeedPickups==null||snapshot.ArmorPickups.Length!=armorPickups.Length||snapshot.SpeedPickups.Length!=speedPickups.Length||snapshot.ArmorPickups.Select(x=>x.InstanceId).Distinct().Count()!=armorPickups.Length||snapshot.SpeedPickups.Select(x=>x.InstanceId).Distinct().Count()!=speedPickups.Length)throw new ArgumentException("Invalid pickup instance set");
            foreach(var item in armorPickups){var state=item.Read();var next=snapshot.ArmorPickups.SingleOrDefault(x=>x.InstanceId==state.InstanceId);if(next.InstanceId!=state.InstanceId||next.Anchor!=state.Anchor||double.IsNaN(next.RespawnRemaining)||double.IsInfinity(next.RespawnRemaining)||next.RespawnRemaining<0)throw new ArgumentException("Invalid armor instance");}
            foreach(var item in speedPickups){var state=item.Read();var next=snapshot.SpeedPickups.SingleOrDefault(x=>x.InstanceId==state.InstanceId);if(next.InstanceId!=state.InstanceId||next.Anchor!=state.Anchor||double.IsNaN(next.Remaining)||double.IsInfinity(next.Remaining)||next.Remaining<0||(!next.Available&&next.Remaining==0))throw new ArgumentException("Invalid speed instance");}
            if(snapshot.HealPickups==null||snapshot.HealPickups.Length!=healPickups.Length||snapshot.HealPickups.Select(x=>x.InstanceId).Distinct().Count()!=healPickups.Length)throw new ArgumentException("Invalid heal instance set");
            foreach(var item in healPickups)
            {
                var next=snapshot.HealPickups.SingleOrDefault(x=>x.InstanceId==item.Read().InstanceId);item.ValidateSnapshot(next);
            }
            for(int i=0;i<lives.Length;i++){if(double.IsNaN(snapshot.SpeedRemaining[i])||double.IsInfinity(snapshot.SpeedRemaining[i])||snapshot.SpeedRemaining[i]<0)throw new ArgumentException("Invalid speed snapshot",nameof(snapshot));lives[i].Restore(snapshot.Lives[i]);speedRemaining[i]=snapshot.SpeedRemaining[i];}
            if(snapshot.DamageRemaining!=null)
            {
                if(snapshot.DamageRemaining.Length!=lives.Length)throw new ArgumentException("Invalid damage snapshot");
                for(int i=0;i<lives.Length;i++){if(double.IsNaN(snapshot.DamageRemaining[i])||double.IsInfinity(snapshot.DamageRemaining[i])||snapshot.DamageRemaining[i]<0)throw new ArgumentException("Invalid damage snapshot");damageRemaining[i]=snapshot.DamageRemaining[i];}
                damagePickup.Restore(snapshot.DamagePickup);
            }
            foreach(var item in healPickups)item.Restore(snapshot.HealPickups.Single(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in armorPickups)item.Restore(snapshot.ArmorPickups.Single(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in speedPickups)item.Restore(snapshot.SpeedPickups.Single(x=>x.InstanceId==item.Read().InstanceId));Time=snapshot.Time;shotSequence=snapshot.ShotSequence;ShotCount=snapshot.ShotCount;
            ResetPickupContacts();
            for(int i=0;i<lives.Length;i++)motors[i].SetHorizontalSpeedMultiplier(speedRemaining[i]>0?speedPickup.Multiplier:1);

        }
        public event Action<DeathNotice> Died;
        public event Action<int> Respawned;
        public event Action<int, float> Fired;
        public event Action<ShotNotice> ShotResolved;
        public NativeCombatSession(CharacterMotor[] motors, ProvingArena arena, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile lifecycle, ProvingProfile combat, NativeMatchState match = null,LabRevisionReference designProfile = null)
        {
            if(match != null && match.Roster.Count != motors.Length) throw new ArgumentException("Roster/motor count mismatch");
            this.designProfile=designProfile?.Clone();
            ArenaIdentity=arena.Definition.Identity;
            this.motors = motors; this.physics = physics; Match=match;
            this.movement = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(movement));
            var tuning = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));
            resolver = new ShotgunResolver(tuning, movement.Get("player.capsule.height"));
            Spawns = new SafeSpawnSelector(physics, arena, this.movement, tuning);
            lives = new CombatLife[motors.Length]; releaseRequired = new bool[motors.Length];speedRemaining=new double[motors.Length];
            for (int i = 0; i < lives.Length; i++) lives[i] = new CombatLife("player-"+(i+1), lifecycle);
            damagePickup=new DamageBoostPickup(arena.Definition.DamagePickupAnchor,lifecycle,arena.Definition.Pickups.FirstOrDefault(x=>x.Kind==ArenaPickupKind.Damage)?.Id??"damage-disabled");damageRemaining=new double[motors.Length];damageWasOutside=new bool[motors.Length];HasDamagePickup=arena.Definition.HasDamagePickup;HasSpeedPickup=!arena.Definition.DisableSpeedPickup;
            healPickups=arena.Definition.Pickups.Where(x=>x.Kind==ArenaPickupKind.FullHeal).Select(x=>new FullHealPickup(x.Anchor,lifecycle,x.Id)).ToArray();
            armorPickups=arena.Definition.Pickups.Where(x=>x.Kind==ArenaPickupKind.Armor).Select(x=>new ArmorPickup(x.Anchor,lifecycle,x.Id)).ToArray();
            speedPickups=arena.Definition.Pickups.Where(x=>x.Kind==ArenaPickupKind.Speed).Select(x=>new SpeedPickup(x.Anchor,lifecycle,x.Id)).ToArray();
            armorPickupWasOutside=new bool[armorPickups.Length,motors.Length];speedPickupWasOutside=new bool[speedPickups.Length,motors.Length];
            ResetPickupContacts();

        }
        void ResetPickupContacts()
        {
            for(int i=0;i<motors.Length;i++)
            {
                damageWasOutside[i]=!damagePickup.InRange(Pose(i).Position);
                for(int k=0;k<armorPickups.Length;k++)armorPickupWasOutside[k,i]=!armorPickups[k].InRange(Pose(i).Position);
                for(int k=0;k<speedPickups.Length;k++)speedPickupWasOutside[k,i]=!speedPickups[k].InRange(Pose(i).Position);
            }
        }
        bool PickupReach(Vector3 feet,Vector3 anchor)
        {
            // Both endpoints are lifted by capsule radius to avoid a coplanar floor ray.
            var delta=anchor-feet;
            return !physics.Raycast(feet+Vector3.up*movement.Get("player.capsule.radius"),delta.normalized,out _,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
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
            foreach(var item in healPickups)item.Advance(seconds);
            foreach(var item in armorPickups)item.Advance(seconds);
            if(HasSpeedPickup)foreach(var item in speedPickups)item.Advance(seconds);if(HasDamagePickup)damagePickup.Advance(seconds);
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i].Advance(seconds);
                damageRemaining[i]=Math.Max(0,damageRemaining[i]-seconds);
                speedRemaining[i]=Math.Max(0,speedRemaining[i]-seconds); motors[i].SetHorizontalSpeedMultiplier(speedRemaining[i]>0?speedPickup.Multiplier:1);
                if (!Life(i).Dead) motors[i].Tick(actions[i], seconds);
            }
            Physics.SyncTransforms();
            for(int i=0;i<lives.Length;i++)
            {
                foreach(var item in healPickups)if(!Life(i).Dead&&item.InRange(Pose(i).Position)&&PickupReach(Pose(i).Position,item.Read().Anchor))item.TryCollect(Pose(i).Position,lives[i]);
                bool damageOutside=!damagePickup.InRange(Pose(i).Position);
                if(HasDamagePickup&&!Life(i).Dead&&damageWasOutside[i]&&!damageOutside&&PickupReach(Pose(i).Position,damagePickup.Read().Anchor)&&damagePickup.TryCollect(Pose(i).Position))damageRemaining[i]=damagePickup.Duration;
                damageWasOutside[i]=damageOutside;
                for(int k=0;k<armorPickups.Length;k++)
                {
                    var item=armorPickups[k];bool outside=!item.InRange(Pose(i).Position);
                    if(armorPickupWasOutside[k,i]&&!outside&&PickupReach(Pose(i).Position,item.Read().Anchor))item.TryCollect(Pose(i).Position,lives[i]);
                    armorPickupWasOutside[k,i]=outside;
                }
                for(int k=0;k<speedPickups.Length;k++)
                {
                    var item=speedPickups[k];bool outside=!item.InRange(Pose(i).Position);
                    if(HasSpeedPickup&&!Life(i).Dead&&speedPickupWasOutside[k,i]&&!outside&&PickupReach(Pose(i).Position,item.Read().Anchor)&&item.TryCollect(Pose(i).Position)){speedRemaining[i]=item.Duration;motors[i].SetHorizontalSpeedMultiplier(item.Multiplier);}
                    speedPickupWasOutside[k,i]=outside;
                }

            }
            var targets=LiveTargets();
            var shots=new List<(int shooter, int life, ShotNotice notice, float[] damage)>();
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i].Select(actions[i].SelectWeapon); // Selection is authoritative before fire on this tick.
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
                    applied+=ApplyDamage(targets[t].Seat, targets[t].Life, shot.damage[t]/resolver.ProjectileCount(shot.notice.Weapon), shot.shooter, shot.life).Applied;
                ShotResolved?.Invoke(shot.notice);
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
        (ShotNotice notice,float[] damage) ResolveShot(int shooter, CombatTarget[] targets)
        {
            ShotCount++; shotSequence++;
            var damage = new float[targets.Length];
            var pose = Pose(shooter); var aim = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            var origin = pose.Position + Vector3.up*movement.Get("camera.eyeHeight");
            var weapon=Life(shooter).SelectedWeapon;
            var pellets=new PelletNotice[resolver.ProjectileCount(weapon)];
            for (int pellet = 0; pellet < pellets.Length; pellet++)
            {
                Vector3 direction = resolver.Direction(aim, pellet, shotSequence,weapon);
                float range=resolver.RangeFor(weapon);
                bool worldHit=physics.Raycast(origin,direction,out var wall,range,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
                float limit=worldHit ? wall.distance : range;
                var hit = resolver.Resolve(origin, direction, targets, shooter, limit,weapon);
                if(hit.TargetIndex>=0)
                {
                    var target=targets[hit.TargetIndex];
                    pellets[pellet]=new PelletNotice(direction,origin+direction*hit.Distance,-direction,PelletContact.Participant,target.Seat,target.Life);
                    if(!Allied(shooter,target.Seat)) damage[hit.TargetIndex]+=resolver.FullDamage(hit.Zone,weapon)*(damageRemaining[shooter]>0?damagePickup.Multiplier:1);
                }
                else if(worldHit) pellets[pellet]=new PelletNotice(direction,wall.point,wall.normal,PelletContact.World);
                else pellets[pellet]=new PelletNotice(direction,origin+direction*range,-direction,PelletContact.Miss);
            }
            return (new ShotNotice(shotSequence,shooter,Life(shooter).Life,Time,origin,pellets,weapon),damage);
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
                damageRemaining[seat]=0;speedRemaining[seat]=0;motors[seat].SetHorizontalSpeedMultiplier(1);
                motors[seat].SetAlive(false);
                releaseRequired[seat] = true;
                Died?.Invoke(new DeathNotice(seat, Life(seat), Pose(seat)));
            }
            return result;
        }
    }
}
