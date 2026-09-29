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
        public readonly FatalImpact Impact;
        public DeathNotice(int seat, CombatLifeState life, ParticipantState pose,FatalImpact impact=default) { Seat=seat; Life=life; Pose=pose; Impact=impact; }
    }
    /// <summary>Actual post-policy losses; presentation cannot infer armor hits from health alone.</summary>
    public readonly struct DamageNotice
    {
        public readonly int Participant, Life;
        public readonly float HealthLost, ArmorLost;
        public readonly double Time;
        public DamageNotice(int participant,int life,float healthLost,float armorLost,double time)
        { Participant=participant;Life=life;HealthLost=healthLost;ArmorLost=armorLost;Time=time; }
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
    [Serializable] public sealed class NativeCombatSessionSnapshot { public int Version=6; public NativeMatchSnapshot Match; public WeaponPickupState[] WeaponPickups; public CutterBeamState[] Beams; public bool[] BeamReleaseRequired; public RocketState[] Rockets; public FullHealPickupState[] HealPickups; public string ArenaIdentity; public LabRevisionReference DesignProfile; public double Time; public uint ShotSequence; public int ShotCount; public CombatLifeState[] Lives; public ArmorPickupState[] ArmorPickups; public SpeedPickupState[] SpeedPickups; public double[] SpeedRemaining; public DamageBoostPickupState DamagePickup; public double[] DamageRemaining; }
    /// <summary>Scene adapter. Only the owner calls Tick while Running. Core remains renderer/device-free.</summary>
    public sealed class NativeCombatSession
    {
        readonly CharacterMotor[] motors;
        readonly CutterBeam[] beams;
        readonly ProvingProfile cutter;
        public ProvingProfile CutterProfile=>JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(cutter));
        public CutterBeamState Beam(int seat)=>beams[seat].Read();
        readonly CombatLife[] lives;
        readonly PhysicsScene physics;
        readonly ProvingArena arena;
        readonly float pickupHeightTolerance;
        readonly ShotgunResolver resolver;
        readonly RocketResolver rocketResolver;
        readonly List<RocketState> rockets=new List<RocketState>();
        public RocketState[] Rockets => rockets.ToArray();
        public event Action<RocketExplosion> RocketExploded;
        public event Action StateRestored;
        readonly ProvingProfile movement;
        readonly bool[] releaseRequired;
        readonly bool[,] armorPickupWasOutside;
        readonly ArmorPickup[] armorPickups;
        ArmorPickup armorPickup=>armorPickups[0];
        public ArmorPickupState[] ArmorPickups=>Array.ConvertAll(armorPickups,x=>x.Read());
        readonly WeaponPickup[] weaponPickups;
        public WeaponPickupState[] WeaponObjectives(int seat)=>weaponPickups.Where(x=>x.Read().Available&&lives[seat].NeedsWeapon(x.Read().Weapon)).Select(x=>x.Read()).ToArray();
        public WeaponPickupState[] WeaponPickups=>Array.ConvertAll(weaponPickups,x=>x.Read());
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
        public double SpeedBoostRemaining(int seat)=>speedRemaining[seat];
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
        public NativeCombatSessionSnapshot Capture() => new NativeCombatSessionSnapshot { Match=Match?.Read(), WeaponPickups=WeaponPickups, BeamReleaseRequired=(bool[])releaseRequired.Clone(),Beams=Array.ConvertAll(beams,b=>b.Read()),DesignProfile=DesignProfile,Rockets=Rockets,HealPickups=HealPickups,ArenaIdentity=ArenaIdentity,ArmorPickups=ArmorPickups,SpeedPickups=SpeedPickups,Time=Time,ShotSequence=shotSequence,ShotCount=ShotCount,Lives=Array.ConvertAll(lives,life=>life.Read()),SpeedRemaining=(double[])speedRemaining.Clone(),DamagePickup=damagePickup.Read(),DamageRemaining=(double[])damageRemaining.Clone() };
        public void Restore(NativeCombatSessionSnapshot snapshot)
        {
            if(snapshot==null||(designProfile!=null&&!designProfile.Matches(snapshot.DesignProfile))||snapshot.Version!=6||(snapshot.ArenaIdentity!=null&&snapshot.ArenaIdentity!=ArenaIdentity)||double.IsNaN(snapshot.Time)||double.IsInfinity(snapshot.Time)||snapshot.Time<0||snapshot.ShotCount<0||snapshot.Lives==null||snapshot.Lives.Length!=lives.Length||snapshot.SpeedRemaining==null||snapshot.SpeedRemaining.Length!=lives.Length)throw new ArgumentException("Invalid native combat snapshot",nameof(snapshot));
            // JsonUtility materializes a null nested Match as an empty object after round-trip.
            bool omittedMatch=Match==null && snapshot.Match!=null && string.IsNullOrEmpty(snapshot.Match.TuningIdentity) && (snapshot.Match.Standings==null || snapshot.Match.Standings.Length==0);
            if(snapshot.Match!=null && !omittedMatch){if(Match==null)throw new ArgumentException("Missing match reducer");Match.ValidateSnapshot(snapshot.Match);}
            if(snapshot.ArmorPickups==null||snapshot.SpeedPickups==null||snapshot.ArmorPickups.Length!=armorPickups.Length||snapshot.SpeedPickups.Length!=speedPickups.Length||snapshot.ArmorPickups.Select(x=>x.InstanceId).Distinct().Count()!=armorPickups.Length||snapshot.SpeedPickups.Select(x=>x.InstanceId).Distinct().Count()!=speedPickups.Length)throw new ArgumentException("Invalid pickup instance set");
            if(snapshot.WeaponPickups==null||snapshot.WeaponPickups.Length!=weaponPickups.Length||snapshot.WeaponPickups.Select(x=>x.InstanceId).Distinct().Count()!=weaponPickups.Length)throw new ArgumentException("Invalid weapon pickup set");
            foreach(var item in weaponPickups)item.ValidateSnapshot(snapshot.WeaponPickups.SingleOrDefault(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in armorPickups){var state=item.Read();var next=snapshot.ArmorPickups.SingleOrDefault(x=>x.InstanceId==state.InstanceId);if(next.InstanceId!=state.InstanceId||next.Anchor!=state.Anchor||double.IsNaN(next.RespawnRemaining)||double.IsInfinity(next.RespawnRemaining)||next.RespawnRemaining<0)throw new ArgumentException("Invalid armor instance");}
            foreach(var item in speedPickups){var state=item.Read();var next=snapshot.SpeedPickups.SingleOrDefault(x=>x.InstanceId==state.InstanceId);if(next.InstanceId!=state.InstanceId||next.Anchor!=state.Anchor||double.IsNaN(next.Remaining)||double.IsInfinity(next.Remaining)||next.Remaining<0||(!next.Available&&next.Remaining==0))throw new ArgumentException("Invalid speed instance");}
            if(snapshot.Rockets==null||snapshot.Rockets.Select(x=>x.Id).Distinct().Count()!=snapshot.Rockets.Length)throw new ArgumentException("Invalid rocket set");
            if(snapshot.Beams!=null){if(snapshot.Beams.Length!=beams.Length)throw new ArgumentException("Invalid beams");for(int i=0;i<beams.Length;i++)beams[i].ValidateSnapshot(snapshot.Beams[i]);}
            if(snapshot.BeamReleaseRequired!=null&&snapshot.BeamReleaseRequired.Length!=beams.Length)throw new ArgumentException("Invalid beam release state");
            foreach(var r in snapshot.Rockets)rocketResolver.Validate(r,lives.Length,snapshot.ShotSequence);
            foreach(var r in snapshot.Rockets)if(r.OwnerLife>snapshot.Lives[r.Owner].Life)throw new ArgumentException("Rocket owner life is in the future");
            for(int i=0;i<lives.Length;i++)lives[i].ValidateSnapshot(snapshot.Lives[i]);
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
            foreach(var item in weaponPickups)item.Restore(snapshot.WeaponPickups.Single(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in healPickups)item.Restore(snapshot.HealPickups.Single(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in armorPickups)item.Restore(snapshot.ArmorPickups.Single(x=>x.InstanceId==item.Read().InstanceId));
            foreach(var item in speedPickups)item.Restore(snapshot.SpeedPickups.Single(x=>x.InstanceId==item.Read().InstanceId));Time=snapshot.Time;shotSequence=snapshot.ShotSequence;ShotCount=snapshot.ShotCount;
            if(snapshot.Beams!=null){if(snapshot.Beams.Length!=beams.Length)throw new ArgumentException("Invalid beams");for(int i=0;i<beams.Length;i++)beams[i].Restore(snapshot.Beams[i]);}
            else foreach(var beam in beams)beam.Reset();
            if(snapshot.BeamReleaseRequired!=null)Array.Copy(snapshot.BeamReleaseRequired,releaseRequired,beams.Length);
            if(snapshot.Match!=null && !omittedMatch)Match.Restore(snapshot.Match);
            rockets.Clear();rockets.AddRange(snapshot.Rockets);StateRestored?.Invoke();
            ResetPickupContacts();
            for(int i=0;i<lives.Length;i++){motors[i].SetAlive(!Life(i).Dead);motors[i].SetHorizontalSpeedMultiplier(speedRemaining[i]>0?speedPickup.Multiplier:1);}

        }
        public event Action<DamageNotice> Damaged;
        public event Action<DeathNotice> Died;
        public event Action<int> Respawned;
        public event Action<int, float> Fired;
        public event Action<int,string,NativeBotPickupKind> PickupCollected;
        public event Action<ShotNotice> ShotResolved;
        public NativeCombatSession(CharacterMotor[] motors, ProvingArena arena, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile lifecycle, ProvingProfile combat, NativeMatchState match = null,LabRevisionReference designProfile = null, ProvingProfile cutterProfile=null)
        {
            if(match != null && match.Roster.Count != motors.Length) throw new ArgumentException("Roster/motor count mismatch");
            this.designProfile=designProfile?.Clone();
            cutter=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(cutterProfile??ProvingProfile.CreateCutterDefault()));
            beams=new CutterBeam[motors.Length];for(int i=0;i<beams.Length;i++)beams[i]=new CutterBeam(cutter,motors.Length);
            ArenaIdentity=arena.Definition.Identity;
            pickupHeightTolerance=lifecycle.Get("pickup.maximumHeightDifference");this.arena=arena;this.motors = motors; this.physics = physics; Match=match;
            this.movement = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(movement));
            var tuning = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));
            tuning.EnsureNativeCombatDescriptors();
            resolver = new ShotgunResolver(tuning, movement.Get("player.capsule.height"));
            rocketResolver = new RocketResolver(tuning,this.movement);
            Spawns = new SafeSpawnSelector(physics, arena, this.movement, tuning);
            lives = new CombatLife[motors.Length]; releaseRequired = new bool[motors.Length];speedRemaining=new double[motors.Length];
            for (int i = 0; i < lives.Length; i++) lives[i] = new CombatLife("player-"+(i+1), lifecycle,cutter);
            damagePickup=new DamageBoostPickup(arena.Definition.DamagePickupAnchor,lifecycle,arena.Definition.Pickups.FirstOrDefault(x=>x.Kind==ArenaPickupKind.Damage)?.Id??"damage-disabled");damageRemaining=new double[motors.Length];damageWasOutside=new bool[motors.Length];HasDamagePickup=arena.Definition.HasDamagePickup;HasSpeedPickup=!arena.Definition.DisableSpeedPickup;
            weaponPickups=arena.Definition.Pickups.Where(x=>WeaponPickup.IsWeapon(x.Kind)).Select(x=>new WeaponPickup(x,lifecycle)).ToArray();
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
        string PickupSupport(Vector3 feet)
        {
            // Project only within the pickup height tolerance; small jumps retain their named floor.
            float radius=movement.Get("player.capsule.radius");
            if(physics.Raycast(feet+Vector3.up*radius,Vector3.down,out var hit,radius+pickupHeightTolerance,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))
                return arena.NavigationSupport(hit.collider);
            return null;
        }
        bool PickupReach(Vector3 feet,Vector3 anchor)
        {
            // Both endpoints are lifted by capsule radius to avoid a coplanar floor ray.
            var delta=anchor-feet;
            return !physics.Raycast(feet+Vector3.up*movement.Get("player.capsule.radius"),delta.normalized,out _,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
        }
        public double WeaponSwitchSeconds => lives[0].SwitchSeconds;
        public CombatLifeState[] LifeStates=>Array.ConvertAll(lives,life=>life.Read());
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
            for (int i = 0; i < lives.Length; i++) { lives[i].ClearHeldInput(); beams[i].Stop(); releaseRequired[i] = true; }
        }
        public void Tick(LocalAction[] actions, float seconds)
        {
            if (actions.Length != motors.Length || seconds <= 0 || (float.IsNaN(seconds) || float.IsInfinity(seconds))) throw new ArgumentException("Invalid tick");
            if(stopped || Match?.Phase==NativeMatchPhase.Finished) return;
            Match?.BeginTick();
            Time += seconds;
            foreach(var item in weaponPickups)item.Advance(seconds);
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
                foreach(var item in weaponPickups)
                    if(!Life(i).Dead&&item.InRange(Pose(i).Position)&&PickupReach(Pose(i).Position,item.Read().Anchor))
                        if(item.TryCollect(Pose(i).Position,PickupSupport(Pose(i).Position),lives[i]))PickupCollected?.Invoke(i,item.Read().InstanceId,NativeBotPickupKind.Weapon);
                foreach(var item in healPickups)if(!Life(i).Dead&&item.InRange(Pose(i).Position)&&PickupReach(Pose(i).Position,item.Read().Anchor))if(item.TryCollect(Pose(i).Position,lives[i]))PickupCollected?.Invoke(i,item.Read().InstanceId,NativeBotPickupKind.Heal);
                bool damageOutside=!damagePickup.InRange(Pose(i).Position);
                if(HasDamagePickup&&!Life(i).Dead&&damageWasOutside[i]&&!damageOutside&&PickupReach(Pose(i).Position,damagePickup.Read().Anchor)&&damagePickup.TryCollect(Pose(i).Position)){damageRemaining[i]=damagePickup.Duration;PickupCollected?.Invoke(i,damagePickup.Read().InstanceId,NativeBotPickupKind.Damage);}
                damageWasOutside[i]=damageOutside;
                for(int k=0;k<armorPickups.Length;k++)
                {
                    var item=armorPickups[k];bool outside=!item.InRange(Pose(i).Position);
                    if(armorPickupWasOutside[k,i]&&!outside&&PickupReach(Pose(i).Position,item.Read().Anchor))if(item.TryCollect(Pose(i).Position,lives[i]))PickupCollected?.Invoke(i,item.Read().InstanceId,NativeBotPickupKind.Armor);
                    armorPickupWasOutside[k,i]=outside;
                }
                for(int k=0;k<speedPickups.Length;k++)
                {
                    var item=speedPickups[k];bool outside=!item.InRange(Pose(i).Position);
                    if(HasSpeedPickup&&!Life(i).Dead&&speedPickupWasOutside[k,i]&&!outside&&PickupReach(Pose(i).Position,item.Read().Anchor)&&item.TryCollect(Pose(i).Position)){PickupCollected?.Invoke(i,item.Read().InstanceId,NativeBotPickupKind.Speed);speedRemaining[i]=item.Duration;motors[i].SetHorizontalSpeedMultiplier(item.Multiplier);}
                    speedPickupWasOutside[k,i]=outside;
                }

            }
            var targets=LiveTargets();
            var shots=new List<(int shooter, int life, ShotNotice notice, float[] damage)>();
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i].Select(actions[i].SelectWeapon); // Selection is authoritative before fire on this tick.
                bool held = actions[i].FireHeld || actions[i].Fire;
                if(Life(i).SelectedWeapon!=WeaponId.Cutter||Life(i).SwitchRemaining>0||Life(i).Dead||!held)beams[i].Stop();
                if (releaseRequired[i])
                {
                    if (!held) releaseRequired[i] = false;
                    lives[i].Fire(false); continue;
                }
                if(Life(i).SelectedWeapon==WeaponId.Cutter)
                {
                    if(actions[i].Fire)beams[i].Stop();
                    lives[i].Fire(held);
                    if(held&&!Life(i).Dead&&Life(i).SwitchRemaining==0)AdvanceBeam(i,seconds);
                    continue;
                }
                // A newly captured physical edge also records an intervening release between ticks.
                if (actions[i].Fire) lives[i].ClearHeldInput();
                if (lives[i].Fire(held))
                {
                    if(Life(i).SelectedWeapon==WeaponId.RocketLauncher)
                    { LaunchRocket(i); continue; }
                    var result=ResolveShot(i,targets);
                    shots.Add((i,Life(i).Life,result.notice,result.damage));
                }
            }
            foreach(var shot in shots)
            {
                float applied=0;
                for(int t=0;t<targets.Length;t++) if(shot.damage[t]>0)
                    applied+=ApplyDamagePolicy(targets[t].Seat, targets[t].Life, shot.damage[t]/resolver.ProjectileCount(shot.notice.Weapon), shot.shooter, shot.life,false,FatalImpact.FromShot(shot.notice,targets[t].Seat,targets[t].Life)).Applied;
                ShotResolved?.Invoke(shot.notice);
                Fired?.Invoke(shot.shooter,applied);
            }
            AdvanceRockets(seconds);
            Match?.EndTick();
            if(Match?.Phase==NativeMatchPhase.Finished) return;
            for (int i = 0; i < lives.Length; i++) if (lives[i].ReadyToRespawn)
            {
                if (!Spawns.TryChoose(LiveTargets(), out var spawn))
                    throw new InvalidOperationException("Fixed arena has no physically safe respawn slot");
                if (lives[i].Respawn(Life(i).Life))
                {
                    beams[i].Reset();
                    motors[i].Initialize(movement, spawn);
                    releaseRequired[i] = actions[i].FireHeld || actions[i].Fire;
                    Physics.SyncTransforms(); // Reserve this live capsule before the next participant chooses.
                    Respawned?.Invoke(i);
                }
            }
        }
        void AdvanceBeam(int shooter,float seconds)
        {
            var pose=Pose(shooter);var direction=Quaternion.Euler(pose.Pitch,pose.Yaw,0)*Vector3.forward;
            var origin=pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
            float limit=float.PositiveInfinity;
            if(physics.Raycast(origin,direction,out var wall,beams[shooter].Range,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore))limit=wall.distance;
            var distances=new float[lives.Length];var identities=new int[lives.Length];
            float radius=movement.Get("player.capsule.radius"),height=movement.Get("player.capsule.height");
            for(int i=0;i<lives.Length;i++)
            {
                identities[i]=Life(i).Life;
                distances[i]=i==shooter||Life(i).Dead||Allied(shooter,i)?-1:RocketResolver.Capsule(origin-Pose(i).Position,direction,radius,height-radius,radius+cutter.Get("cutter.hitRadius"));
            }
            bool began=!beams[shooter].Read().Active&&Life(shooter).CutterEnergy>0;float applied=0;
            if(began){ShotCount++;shotSequence++;ShotResolved?.Invoke(new ShotNotice(shotSequence,shooter,Life(shooter).Life,Time,origin,Array.Empty<PelletNotice>(),WeaponId.Cutter));}
            double spent=beams[shooter].Advance(seconds,Life(shooter).CutterEnergy,origin,direction,limit,distances,identities,
                damageRemaining[shooter]>0?damagePickup.Multiplier:1,(target,amount)=>applied+=ApplyDamagePolicy(target,identities[target],amount,shooter,Life(shooter).Life,false,new FatalImpact(WeaponId.Cutter,shotSequence,direction,origin+direction*Mathf.Max(0,distances[target]))).Applied);
            lives[shooter].ConsumeCutter(spent);
            if(began||applied>0)Fired?.Invoke(shooter,applied);
            if(Life(shooter).CutterEnergy<=0)beams[shooter].Stop();
        }
        void LaunchRocket(int shooter)
        {
            var pose=Pose(shooter);var aim=Quaternion.Euler(pose.Pitch,pose.Yaw,0);
            ShotCount++;shotSequence=checked(shotSequence+1);
            rockets.Add(new RocketState{Id=shotSequence,Owner=shooter,OwnerLife=Life(shooter).Life,
                Position=pose.Position+Vector3.up*movement.Get("camera.eyeHeight"),Direction=aim*Vector3.forward,
                DamageMultiplier=damageRemaining[shooter]>0?damagePickup.Multiplier:1});
            // Launch is a shot event; delayed damage comes only from RocketExploded, never from hitscan.
            ShotResolved?.Invoke(new ShotNotice(shotSequence,shooter,Life(shooter).Life,Time,rockets[rockets.Count-1].Position,Array.Empty<PelletNotice>(),WeaponId.RocketLauncher));
            Fired?.Invoke(shooter,0);
        }
        void AdvanceRockets(float seconds)
        {
            for(int index=0;index<rockets.Count;)
            {
                var r=rockets[index];float travel=Mathf.Min(rocketResolver.Speed*seconds,rocketResolver.Range-r.Distance);
                bool wallHit=physics.Raycast(r.Position,r.Direction,out var wall,travel,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
                var targets=LiveTargets();
                var contact=rocketResolver.Contact(r.Position,r.Direction,wallHit?wall.distance:travel+Mathf.Epsilon,targets,r.Owner,r.OwnerLife);
                bool direct=contact.TargetIndex>=0;
                if(direct||wallHit)
                {
                    var center=direct?r.Position+r.Direction*contact.Distance:wall.point+wall.normal*movement.Get("player.capsule.skinWidth");
                    int directSeat=direct?targets[contact.TargetIndex].Seat:-1;
                    rockets.RemoveAt(index); // Remove before callbacks: one identity cannot explode twice.
                    foreach(var target in targets)
                    {
                        bool directTarget=target.Seat==directSeat;
                        var point=rocketResolver.ClosestPoint(center,target.Pose.Position);var delta=point-center;
                        if(!directTarget&&(delta.magnitude>=rocketResolver.Radius||delta.sqrMagnitude>0&&physics.Raycast(center,delta.normalized,out _,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore)))continue;
                        float damage=rocketResolver.Damage(delta.magnitude,directTarget,r.DamageMultiplier);
                        ApplyDamagePolicy(target.Seat,target.Life,damage,r.Owner,r.OwnerLife,true,new FatalImpact(WeaponId.RocketLauncher,r.Id,(target.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight")-center).normalized,center,Mathf.Clamp01(delta.magnitude/rocketResolver.Radius)));
                    }
                    RocketExploded?.Invoke(new RocketExplosion(r,center,Time,directSeat));
                }
                else
                {
                    r.Position+=r.Direction*travel;r.Distance+=travel;
                    if(r.Distance>=rocketResolver.Range)rockets.RemoveAt(index);
                    else {rockets[index]=r;index++;}
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
            => ApplyDamagePolicy(seat,life,damage,killer,killerLife,false);
        DamageResult ApplyDamagePolicy(int seat,int life,float damage,int killer,int killerLife,bool rocket,FatalImpact impact=default)
        {
            if(stopped || Match?.Phase==NativeMatchPhase.Finished) return default;
            if(!rocket && killer >= 0 && Allied(killer,seat)) return default;
            var before=Life(seat);
            var result = lives[seat].Damage(life, damage, killer < 0 ? null : Life(killer).ParticipantId, killerLife);
            var after=Life(seat);
            float healthLost=before.Health-after.Health,armorLost=before.Armor-after.Armor;
            if(healthLost>0||armorLost>0)Damaged?.Invoke(new DamageNotice(seat,after.Life,healthLost,armorLost,Time));
            // Friendly splash can kill, but cannot earn enemy kill/assist rewards.
            int scorer=killer>=0&&killer!=seat&&Allied(killer,seat)?-1:killer;
            Match?.RecordDamage(seat,scorer,result,killer<0||(!Life(killer).Dead&&Life(killer).Life==killerLife),originalSource:killer);
            if (result.Killed)
            {
                var deathPose=Pose(seat); // Capture momentum before SetAlive clears motor velocity.
                beams[seat].Stop();
                damageRemaining[seat]=0;speedRemaining[seat]=0;motors[seat].SetHorizontalSpeedMultiplier(1);
                motors[seat].SetAlive(false);
                releaseRequired[seat] = true;
                Died?.Invoke(new DeathNotice(seat, Life(seat), deathPose,impact));
            }
            return result;
        }
    }
}
