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
        public int RifleAmmo;
        public int ShotgunAmmo;
        public double RifleCooldownRemaining;
        public double ShotgunCooldownRemaining;
        public bool Dead;
        public bool FireHeld;
        public double CooldownRemaining;
        public double RespawnRemaining;
        public string KillerId;
        public int KillerLife;
    }

    public readonly struct DamageResult
    {
        public readonly float Applied;
        public readonly bool Killed;
        public DamageResult(float applied, bool killed) { Applied = applied; Killed = killed; }
    }

    /// <summary>
    /// Owns one participant's combat lifecycle. No scene, clock, device, or renderer access.
    /// Physics resolves hits upstream; a match adapter ticks this only while running,
    /// clears held input at pause/focus boundaries, and chooses a safe spawn before Respawn.
    /// </summary>
    public sealed class CombatLife
    {
        readonly float maximumHealth, maximumArmor;
        readonly int startingAmmo, rifleStartingAmmo;
        readonly double cooldown, rifleCooldown, respawnDelay, switchSeconds;
        CombatLifeState state;
        public CombatLifeState Read() => state; // Value DTO: readers cannot mutate owned state.
        public bool ReadyToRespawn => state.Dead && state.RespawnRemaining <= 0;

        public CombatLife(string participantId, ProvingProfile profile)
        {
            if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("Participant identity required", nameof(participantId));
            if(profile==null) throw new ArgumentException("Valid unity-combat-state-v1 profile required",nameof(profile));
            profile=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(profile));
            profile.EnsureCombatDescriptors();
            if (profile == null || profile.Id != "unity-combat-state-v1" || profile.Version != 4 || profile.Validate().Count != 0)
                throw new ArgumentException("Valid unity-combat-state-v1 profile required", nameof(profile));
            maximumHealth = profile.Get("combat.maximumHealth");
            maximumArmor = profile.Get("armor.maximum");
            startingAmmo = IntegerAmmo(profile.Get("combat.startingAmmo"));
            rifleStartingAmmo = IntegerAmmo(profile.Get("rifle.startingAmmo"));
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
            ShotgunAmmo=startingAmmo, Ammo=rifleStartingAmmo
        };

        static int IntegerAmmo(float value)
        {
            if (value != Math.Floor(value)) throw new ArgumentException("Ammo must be integral");
            return (int)value;
        }

        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            state.RifleCooldownRemaining = Math.Max(0, state.RifleCooldownRemaining - seconds);
            state.ShotgunCooldownRemaining = Math.Max(0, state.ShotgunCooldownRemaining - seconds);
            if (!state.Dead && state.SwitchRemaining > 0)
            {
                state.SwitchRemaining = Math.Max(0, state.SwitchRemaining - seconds);
                if (state.SwitchRemaining == 0)
                {
                    state.SelectedWeapon = state.PendingWeapon;
                    state.PendingWeapon = default;
                    state.Ammo = state.SelectedWeapon == WeaponId.Rifle ? state.RifleAmmo : state.ShotgunAmmo;
                }
            }
            state.CooldownRemaining = state.SelectedWeapon==WeaponId.Rifle ? state.RifleCooldownRemaining : state.ShotgunCooldownRemaining;
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
                case WeaponSelection.Previous: case WeaponSelection.Next:
                    target=state.SelectedWeapon==WeaponId.Rifle?WeaponId.Shotgun:WeaponId.Rifle; break;
                default: throw new ArgumentOutOfRangeException(nameof(selection));
            }
            if (target == state.SelectedWeapon) return;
            state.PendingWeapon = target;
            state.SwitchRemaining = switchSeconds;
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
        public void Restore(CombatLifeState snapshot)
        {
            if(snapshot.ParticipantId!=state.ParticipantId||snapshot.Life<1||float.IsNaN(snapshot.Health)||float.IsInfinity(snapshot.Health)||snapshot.Health<0||snapshot.Health>maximumHealth||float.IsNaN(snapshot.Armor)||float.IsInfinity(snapshot.Armor)||snapshot.Armor<0||snapshot.Armor>maximumArmor||snapshot.Ammo<0||snapshot.RifleAmmo<0||snapshot.ShotgunAmmo<0||snapshot.SelectedWeapon!=WeaponId.Rifle&&snapshot.SelectedWeapon!=WeaponId.Shotgun||double.IsNaN(snapshot.RifleCooldownRemaining)||double.IsInfinity(snapshot.RifleCooldownRemaining)||snapshot.RifleCooldownRemaining<0||double.IsNaN(snapshot.ShotgunCooldownRemaining)||double.IsInfinity(snapshot.ShotgunCooldownRemaining)||snapshot.ShotgunCooldownRemaining<0||double.IsNaN(snapshot.SwitchRemaining)||double.IsInfinity(snapshot.SwitchRemaining)||snapshot.SwitchRemaining<0||snapshot.SwitchRemaining>switchSeconds||snapshot.SwitchRemaining==0&&snapshot.PendingWeapon!=default||snapshot.SwitchRemaining>0&&(snapshot.PendingWeapon==default||snapshot.PendingWeapon==snapshot.SelectedWeapon||snapshot.PendingWeapon!=WeaponId.Rifle&&snapshot.PendingWeapon!=WeaponId.Shotgun)) throw new ArgumentException("Invalid combat life snapshot",nameof(snapshot));
            if(snapshot.Ammo!=(snapshot.SelectedWeapon==WeaponId.Rifle?snapshot.RifleAmmo:snapshot.ShotgunAmmo))throw new ArgumentException("Selected ammo mismatch",nameof(snapshot));
            state=snapshot;
        }

        public bool Fire(bool held)
        {
            bool edge = held && !state.FireHeld;
            state.FireHeld = held;
            if (state.Dead || state.SwitchRemaining > 0 || !held || state.CooldownRemaining > 0 || state.Ammo == 0 ||
                (state.SelectedWeapon==WeaponId.Shotgun && !edge)) return false;
            if(state.SelectedWeapon==WeaponId.Rifle)
            {
                state.RifleAmmo--; state.RifleCooldownRemaining=rifleCooldown;
                state.Ammo=state.RifleAmmo;
            }
            else
            {
                // One edge consumes one double-barrel shot: a semantic rule, not tuning.
                state.ShotgunAmmo--; state.ShotgunCooldownRemaining=cooldown;
                state.Ammo=state.ShotgunAmmo;
            }
            state.CooldownRemaining = state.SelectedWeapon==WeaponId.Rifle?state.RifleCooldownRemaining:state.ShotgunCooldownRemaining;
            return true;
        }

        public DamageResult Damage(int targetLife, float amount, string killerId = null, int killerLife = 0)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if ((killerId == null && killerLife != 0) || (killerId != null && (string.IsNullOrWhiteSpace(killerId) || killerLife < 1)))
                throw new ArgumentException("Killer requires both participant and life identity");
            if (state.Dead || state.Life != targetLife || amount == 0) return default;
            float absorbed=Math.Min(state.Armor,amount);
            state.Armor-=absorbed;
            float applied = Math.Min(state.Health, amount-absorbed);
            state.Health -= applied;
            bool killed = state.Health <= 0;
            if (killed)
            {
                state.Dead = true;
                state.RespawnRemaining = respawnDelay;
                state.KillerId = killerId;
                state.KillerLife = killerLife;
                state.FireHeld = false;
                state.PendingWeapon = default;
                state.SwitchRemaining = 0;
            }
            return new DamageResult(applied, killed);
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
