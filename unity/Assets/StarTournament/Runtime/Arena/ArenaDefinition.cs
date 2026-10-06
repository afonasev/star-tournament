using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class ArenaSolid { public string Id, Support, Material, Surface; public Vector3 Position, Size; public Quaternion Rotation; public int Layer; }
    [Serializable] public sealed class ArenaSpawnRegion { public string Id, Support; public Vector3 Center, Size; }
    public enum ArenaPickupKind { Armor, Speed, Damage, FullHeal, Shotgun, Pulse, Cutter }
    [Serializable] public sealed class ArenaPickupDefinition { public string Id, Support; public ArenaPickupKind Kind; public Vector3 Anchor; }
    [Serializable] public sealed class ArenaDefinition
    {
        public string Revision, MapId, ProfileFingerprint, Identity;
        public ArenaSolid[] Solids; public Vector3[] Spawns, RouteAnchors;
        public ArenaPickupDefinition[] Pickups;
        public Vector3 ArmorPickupAnchor=>Pickups.First(x=>x.Kind==ArenaPickupKind.Armor).Anchor;
        public Vector3 SpeedPickupAnchor=>Pickups.First(x=>x.Kind==ArenaPickupKind.Speed).Anchor;
        public Vector3 DamagePickupAnchor=>Pickups.FirstOrDefault(x=>x.Kind==ArenaPickupKind.Damage)?.Anchor??Vector3.zero;
        public bool HasDamagePickup=>Pickups.Any(x=>x.Kind==ArenaPickupKind.Damage);
        public bool DisableSpeedPickup=>!Pickups.Any(x=>x.Kind==ArenaPickupKind.Speed);
        public ArenaSpawnRegion[] SpawnRegions; public NativeNavigationTransition[] Transitions;
        public ArenaDefinition Copy() => JsonUtility.FromJson<ArenaDefinition>(JsonUtility.ToJson(this));
    }

    /// <summary>ARENA-5 immutable pre-session hand-off; it is not match or replay state.</summary>
    public sealed class ArenaFreezeSnapshot
    {
        readonly ArenaDefinition definition;
        readonly ProvingProfile profile;
        ArenaFreezeSnapshot(ArenaDefinition definition, ProvingProfile profile)
        { this.definition=definition.Copy(); this.profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile)); }
        public string Identity => definition.Identity;
        public string MapId => definition.MapId;
        public string ProfileFingerprint => definition.ProfileFingerprint;
        public ArenaDefinition Definition => definition.Copy();
        public static ArenaFreezeSnapshot Create(ArenaDefinition definition, ProvingProfile profile)
        {
            if(definition==null || profile==null) throw new ArgumentException("ARENA_FREEZE_MISSING_INPUT|family:unknown|element:snapshot");
            var result=ArenaDefinitionValidator.Validate(definition,profile);
            if(!result.IsValid) throw new ArgumentException(result.ToString());
            if(!definition.ProfileFingerprint.StartsWith("authored:",StringComparison.Ordinal) && definition.ProfileFingerprint!=ProfileFingerprintUtility.Fingerprint(profile))
                throw new ArgumentException($"ARENA_FREEZE_PROFILE_MISMATCH|family:{definition.MapId}|element:profile");
            return new ArenaFreezeSnapshot(definition,profile);
        }
        public void RequireDefinition(ArenaDefinition candidate)
        {
            if(candidate==null || candidate.Identity!=definition.Identity || candidate.MapId!=definition.MapId ||
                candidate.ProfileFingerprint!=definition.ProfileFingerprint || JsonUtility.ToJson(candidate)!=JsonUtility.ToJson(definition))
                throw new ArgumentException($"ARENA_FREEZE_DEFINITION_MISMATCH|family:{MapId}|element:definition");
            if(!ProfileFingerprint.StartsWith("authored:",StringComparison.Ordinal) && ProfileFingerprintUtility.Fingerprint(profile)!=ProfileFingerprint)
                throw new ArgumentException($"ARENA_FREEZE_PROFILE_MISMATCH|family:{MapId}|element:profile");
        }
    }

    public static class ProfileFingerprintUtility
    {
        public static string Fingerprint(ProvingProfile p) => string.Join(";",p.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>d.Path+"="+p.Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
    }
}
