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
        readonly int startingAmmo, refillAmmo;
        readonly double cooldown, respawnDelay;
        CombatLifeState state;
        public CombatLifeState Read() => state; // Value DTO: readers cannot mutate owned state.
        public bool ReadyToRespawn => state.Dead && state.RespawnRemaining <= 0;

        public CombatLife(string participantId, ProvingProfile profile)
        {
            if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("Participant identity required", nameof(participantId));
            if(profile==null) throw new ArgumentException("Valid unity-combat-state-v1 profile required",nameof(profile));
            profile=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(profile));
            profile.EnsureCombatDescriptors();
            if (profile == null || profile.Id != "unity-combat-state-v1" || profile.Version != 1 || profile.Validate().Count != 0)
                throw new ArgumentException("Valid unity-combat-state-v1 profile required", nameof(profile));
            maximumHealth = profile.Get("combat.maximumHealth");
            maximumArmor = profile.Get("armor.maximum");
            startingAmmo = IntegerAmmo(profile.Get("combat.startingAmmo"));
            refillAmmo = IntegerAmmo(profile.Get("combat.emptyRefillAmmo"));
            cooldown = profile.Get("combat.cooldownSeconds");
            respawnDelay = profile.Get("combat.killcamSeconds");
            // Identity counters start at one; zero is the absent-killer sentinel, not balance.
            state = new CombatLifeState { ParticipantId = participantId, Life = 1, Health = maximumHealth, Ammo = startingAmmo };
        }

        static int IntegerAmmo(float value)
        {
            if (value != Math.Floor(value)) throw new ArgumentException("Ammo must be integral");
            return (int)value;
        }

        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            state.CooldownRemaining = Math.Max(0, state.CooldownRemaining - seconds);
            if (state.Dead) state.RespawnRemaining = Math.Max(0, state.RespawnRemaining - seconds);
        }

        public void ClearHeldInput() { state.FireHeld = false; }
        public void GrantArmor(float amount)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<0) throw new ArgumentOutOfRangeException(nameof(amount));
            state.Armor=Math.Min(maximumArmor,state.Armor+amount);
        }
        public void Restore(CombatLifeState snapshot)
        {
            if(snapshot.ParticipantId!=state.ParticipantId||snapshot.Life<1||float.IsNaN(snapshot.Health)||float.IsInfinity(snapshot.Health)||snapshot.Health<0||snapshot.Health>maximumHealth||float.IsNaN(snapshot.Armor)||float.IsInfinity(snapshot.Armor)||snapshot.Armor<0||snapshot.Armor>maximumArmor||snapshot.Ammo<0) throw new ArgumentException("Invalid combat life snapshot",nameof(snapshot));
            state=snapshot;
        }

        public bool Fire(bool held)
        {
            bool edge = held && !state.FireHeld;
            state.FireHeld = held;
            if (!edge || state.Dead || state.CooldownRemaining > 0) return false;
            // One trigger consumes one double-barrel shot: a semantic rule, not tuning.
            state.Ammo--;
            if (state.Ammo == 0) state.Ammo = refillAmmo;
            state.CooldownRemaining = cooldown;
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
            }
            return new DamageResult(applied, killed);
        }

        public bool Respawn(int expectedLife)
        {
            if (!ReadyToRespawn || state.Life != expectedLife) return false;
            int nextLife = checked(state.Life + 1);
            state = new CombatLifeState { ParticipantId = state.ParticipantId, Life = nextLife, Health = maximumHealth, Ammo = startingAmmo };
            return true;
        }
    }
}
