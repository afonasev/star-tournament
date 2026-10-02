using System;

namespace StarTournament.ProvingGround
{
    [Serializable]
    public struct CombatLifeState
    {
        public string ParticipantId;
        public int Life;
        public float Health;
        public float Armor;
        public int Ammo;
        public WeaponId SelectedWeapon;
        public WeaponId PendingWeapon;
        public double SwitchRemaining;
        public bool ShotgunOwned, RocketOwned, CutterOwned;
        public int RifleAmmo;
        public int ShotgunAmmo;
        public int RocketAmmo;
        public double CutterEnergy;
        public double RifleCooldownRemaining;
        public double ShotgunCooldownRemaining;
        public double RocketCooldownRemaining;
        public bool Dead;
        public bool FireHeld;
        public double CooldownRemaining;
        public double RespawnRemaining;
        public string KillerId;
        public int KillerLife;
    }

    public readonly struct DamageResult
    {
        public readonly float Applied, HealthLost, ArmorLost;
        public readonly bool Killed;
        public DamageResult(float applied, bool killed) : this(applied,0,killed) { }
        public DamageResult(float healthLost,float armorLost,bool killed)
        { HealthLost=healthLost;ArmorLost=armorLost;Applied=healthLost+armorLost;Killed=killed; }
    }

    /// <summary>
    /// Owns one participant's combat lifecycle. No scene, clock, device, or renderer access.
    /// Physics resolves hits upstream; a match adapter ticks this only while running,
    /// clears held input at pause/focus boundaries, and chooses a safe spawn before Respawn.
    /// </summary>
    public sealed class CombatLife
    {
        readonly float maximumHealth, maximumArmor;
        readonly double cutterCapacity;
        readonly int startingAmmo, rifleStartingAmmo, rocketStartingAmmo;
        readonly double cooldown, rifleCooldown, rocketCooldown, respawnDelay, switchSeconds;
        CombatLifeState state;
        public double SwitchSeconds => switchSeconds;
        public CombatLifeState Read() => state; // Value DTO: readers cannot mutate owned state.
        public bool ReadyToRespawn => state.Dead && state.RespawnRemaining <= 0;

        public CombatLife(string participantId, ProvingProfile profile, ProvingProfile cutter=null)
        {
            if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("Participant identity required", nameof(participantId));
            if(profile==null) throw new ArgumentException("Valid unity-combat-state-v1 profile required",nameof(profile));
            profile=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(profile));
            profile.EnsureCombatDescriptors();
            if (profile == null || profile.Id != "unity-combat-state-v1" || profile.Version != 7 || profile.Validate().Count != 0)
                throw new ArgumentException("Valid unity-combat-state-v1 profile required", nameof(profile));
            cutterCapacity=(cutter??ProvingProfile.CreateCutterDefault()).Get("cutter.energyCapacity");
            maximumHealth = profile.Get("combat.maximumHealth");
            maximumArmor = profile.Get("armor.maximum");
            startingAmmo = IntegerAmmo(profile.Get("combat.startingAmmo"));
            rifleStartingAmmo = IntegerAmmo(profile.Get("rifle.startingAmmo"));
            rocketStartingAmmo = IntegerAmmo(profile.Get("rocket.startingAmmo"));
            rocketCooldown = profile.Get("rocket.cooldownSeconds");
            cooldown = profile.Get("combat.cooldownSeconds");
            rifleCooldown = profile.Get("rifle.cooldownSeconds");
            switchSeconds = profile.Get("weapon.switchSeconds");
            respawnDelay = profile.Get("combat.killcamSeconds");
            // Identity counters start at one; zero is the absent-killer sentinel, not balance.
            state = NewLife(participantId, 1);
        }

        CombatLifeState NewLife(string participantId, int life) => new CombatLifeState
        {
            ParticipantId=participantId, Life=life, Health=maximumHealth,
            SelectedWeapon=WeaponId.Rifle, RifleAmmo=rifleStartingAmmo,
            Ammo=rifleStartingAmmo
        };

        static int IntegerAmmo(float value)
        {
            if (value != Math.Floor(value)) throw new ArgumentException("Ammo must be integral");
            return (int)value;
        }

        // Numerical tolerance only: float profile/tick conversion can leave nanoseconds after exact tick boundaries.
        static double Countdown(double remaining,double elapsed)
        {double next=remaining-elapsed;return next<=1e-7?0:next;}
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            state.RifleCooldownRemaining = Countdown(state.RifleCooldownRemaining, seconds);
            state.ShotgunCooldownRemaining = Countdown(state.ShotgunCooldownRemaining, seconds);
            state.RocketCooldownRemaining = Countdown(state.RocketCooldownRemaining, seconds);
            if (!state.Dead && state.SwitchRemaining > 0)
            {
                state.SwitchRemaining = Countdown(state.SwitchRemaining, seconds);
                if (state.SwitchRemaining == 0)
                {
                    state.SelectedWeapon = state.PendingWeapon;
                    state.PendingWeapon = default;
                    state.Ammo = AmmoFor(state, state.SelectedWeapon);
                }
            }
            state.CooldownRemaining = CooldownFor(state, state.SelectedWeapon);
            if (state.Dead) state.RespawnRemaining = Math.Max(0, state.RespawnRemaining - seconds);
        }

        public void ClearHeldInput() { state.FireHeld = false; }
        public void Select(WeaponSelection selection)
        {
            if (state.Dead || selection==WeaponSelection.None || state.SwitchRemaining > 0) return;
            WeaponId target;
            switch(selection)
            {
                case WeaponSelection.Rifle: target=WeaponId.Rifle; break;
                case WeaponSelection.Shotgun: target=WeaponId.Shotgun; break;
                case WeaponSelection.RocketLauncher: target=WeaponId.RocketLauncher; break;
                case WeaponSelection.Cutter: target=WeaponId.Cutter; break;
                case WeaponSelection.Previous: target=Cycle(-1); break;
                case WeaponSelection.Next: target=Cycle(1); break;
                default: throw new ArgumentOutOfRangeException(nameof(selection));
            }
            if (target == state.SelectedWeapon || !Owned(state,target)) return;
            state.PendingWeapon = target;
            state.SwitchRemaining = switchSeconds;
        }
        // Four slots are the input protocol, not a tunable balance value.
        WeaponId Cycle(int step)
        {
            var target=state.SelectedWeapon;
            for(int i=0;i<4;i++){target=(WeaponId)(((int)target-1+step+4)%4+1);if(Owned(state,target))return target;}
            return state.SelectedWeapon;
        }
        public static bool Owned(CombatLifeState s,WeaponId id)=>id==WeaponId.Rifle||id==WeaponId.Shotgun&&s.ShotgunOwned||id==WeaponId.RocketLauncher&&s.RocketOwned||id==WeaponId.Cutter&&s.CutterOwned;
        public bool NeedsWeapon(WeaponId id)=>!state.Dead&&(!Owned(state,id)||id==WeaponId.Shotgun&&state.ShotgunAmmo<startingAmmo||id==WeaponId.RocketLauncher&&state.RocketAmmo<rocketStartingAmmo||id==WeaponId.Cutter&&state.CutterEnergy<cutterCapacity);
        public bool CollectWeapon(WeaponId id)
        {
            if(id==WeaponId.Rifle||!WeaponValid(id))throw new ArgumentException("Only map weapons may be collected");
            if(!NeedsWeapon(id))return false;
            if(id==WeaponId.Shotgun){state.ShotgunOwned=true;state.ShotgunAmmo=startingAmmo;}
            else if(id==WeaponId.RocketLauncher){state.RocketOwned=true;state.RocketAmmo=rocketStartingAmmo;}
            else {state.CutterOwned=true;state.CutterEnergy=cutterCapacity;}
            state.Ammo=AmmoFor(state,state.SelectedWeapon);return true;
        }
        public void HealTo(float target)
        {
            if(float.IsNaN(target)||float.IsInfinity(target)||target<0)throw new ArgumentOutOfRangeException(nameof(target));
            if(!state.Dead)state.Health=Math.Max(state.Health,Math.Min(target,maximumHealth));
        }
        public void GrantArmor(float amount)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<0) throw new ArgumentOutOfRangeException(nameof(amount));
            state.Armor=Math.Min(maximumArmor,state.Armor+amount);
        }
        public static int AmmoFor(CombatLifeState s, WeaponId id) => id==WeaponId.Cutter?(int)Math.Ceiling(s.CutterEnergy):id==WeaponId.Rifle?s.RifleAmmo:id==WeaponId.Shotgun?s.ShotgunAmmo:s.RocketAmmo;
        public static double CooldownFor(CombatLifeState s, WeaponId id) => id==WeaponId.Cutter?0:id==WeaponId.Rifle?s.RifleCooldownRemaining:id==WeaponId.Shotgun?s.ShotgunCooldownRemaining:s.RocketCooldownRemaining;
        static bool WeaponValid(WeaponId id) => id>=WeaponId.Rifle && id<=WeaponId.Cutter;
        static bool ValidTimer(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v>=0;
        public void ValidateSnapshot(CombatLifeState snapshot)
        {
            if(!ValidTimer(snapshot.CutterEnergy)||snapshot.CutterEnergy>cutterCapacity||snapshot.ParticipantId!=state.ParticipantId||snapshot.Life<1||float.IsNaN(snapshot.Health)||float.IsInfinity(snapshot.Health)||snapshot.Health<0||snapshot.Health>maximumHealth||float.IsNaN(snapshot.Armor)||float.IsInfinity(snapshot.Armor)||snapshot.Armor<0||snapshot.Armor>maximumArmor||snapshot.Ammo<0||snapshot.RifleAmmo<0||snapshot.ShotgunAmmo<0||snapshot.RocketAmmo<0||!WeaponValid(snapshot.SelectedWeapon)||!ValidTimer(snapshot.RifleCooldownRemaining)||!ValidTimer(snapshot.ShotgunCooldownRemaining)||!ValidTimer(snapshot.RocketCooldownRemaining)||!ValidTimer(snapshot.SwitchRemaining)||snapshot.SwitchRemaining>switchSeconds||snapshot.SwitchRemaining==0&&snapshot.PendingWeapon!=default||snapshot.SwitchRemaining>0&&(!WeaponValid(snapshot.PendingWeapon)||snapshot.PendingWeapon==snapshot.SelectedWeapon))
                throw new ArgumentException("Invalid combat life snapshot",nameof(snapshot));
            if(!Owned(snapshot,snapshot.SelectedWeapon)||snapshot.SwitchRemaining>0&&!Owned(snapshot,snapshot.PendingWeapon)||
                !snapshot.ShotgunOwned&&snapshot.ShotgunAmmo!=0||!snapshot.RocketOwned&&snapshot.RocketAmmo!=0||!snapshot.CutterOwned&&snapshot.CutterEnergy!=0)
                throw new ArgumentException("Invalid weapon ownership",nameof(snapshot));
            if(snapshot.Ammo!=AmmoFor(snapshot,snapshot.SelectedWeapon))throw new ArgumentException("Selected ammo mismatch",nameof(snapshot));
        }
        public void Restore(CombatLifeState snapshot) { ValidateSnapshot(snapshot); state=snapshot; }

        public void ConsumeCutter(double amount)
        { state.CutterEnergy=Math.Max(0,state.CutterEnergy-amount); if(state.SelectedWeapon==WeaponId.Cutter)state.Ammo=AmmoFor(state,WeaponId.Cutter); }

        public bool Fire(bool held)
        {
            bool edge = held && !state.FireHeld;
            state.FireHeld = held;
            if (!Owned(state,state.SelectedWeapon) || state.SelectedWeapon==WeaponId.Cutter || state.Dead || state.SwitchRemaining > 0 || !held || state.CooldownRemaining > 0 || state.Ammo == 0 ||
                (state.SelectedWeapon!=WeaponId.Rifle && !edge)) return false;
            if(state.SelectedWeapon==WeaponId.Rifle)
            {
                state.RifleAmmo--; state.RifleCooldownRemaining=rifleCooldown;
                state.Ammo=state.RifleAmmo;
            }
            else if(state.SelectedWeapon==WeaponId.RocketLauncher)
            {
                state.RocketAmmo--; state.RocketCooldownRemaining=rocketCooldown; state.Ammo=state.RocketAmmo;
            }
            else
            {
                // One edge consumes one double-barrel shot: a semantic rule, not tuning.
                state.ShotgunAmmo--; state.ShotgunCooldownRemaining=cooldown;
                state.Ammo=state.ShotgunAmmo;
            }
            state.CooldownRemaining = CooldownFor(state, state.SelectedWeapon);
            return true;
        }

        public DamageResult Damage(int targetLife, float amount, string killerId = null, int killerLife = 0)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if ((killerId == null && killerLife != 0) || (killerId != null && (string.IsNullOrWhiteSpace(killerId) || killerLife < 1)))
                throw new ArgumentException("Killer requires both participant and life identity");
            if (state.Dead || state.Life != targetLife || amount == 0) return default;
            float beforeHealth=state.Health,beforeArmor=state.Armor;
            float absorbed=Math.Min(state.Armor,amount);
            state.Armor-=absorbed;
            float applied = Math.Min(state.Health, amount-absorbed);
            state.Health -= applied;
            bool killed = state.Health <= 0;
            if (killed)
            {
                state.Dead = true;
                state.ShotgunOwned=state.RocketOwned=state.CutterOwned=false;
                state.ShotgunAmmo=state.RocketAmmo=0;state.CutterEnergy=0;
                state.SelectedWeapon=WeaponId.Rifle;state.Ammo=state.RifleAmmo;
                state.CooldownRemaining=state.RifleCooldownRemaining;
                state.RespawnRemaining = respawnDelay;
                state.KillerId = killerId;
                state.KillerLife = killerLife;
                state.FireHeld = false;
                state.PendingWeapon = default;
                state.SwitchRemaining = 0;
            }
            return new DamageResult(beforeHealth-state.Health, beforeArmor-state.Armor, killed);
        }

        public bool Respawn(int expectedLife)
        {
            if (!ReadyToRespawn || state.Life != expectedLife) return false;
            int nextLife = checked(state.Life + 1);
            state = NewLife(state.ParticipantId,nextLife);
            return true;
        }
    }
}
